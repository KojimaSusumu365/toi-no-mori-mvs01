#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd -- "$SCRIPT_DIR/.." && pwd)"
TOOLS_ROOT="$PROJECT_ROOT/.tools"
DOWNLOAD_ROOT="${MVS01_TOOL_DOWNLOAD_DIR:-/tmp/toi-no-mori-tool-downloads}"
BUILD_ROOT="$(mktemp -d /tmp/toi-no-mori-toolchain.XXXXXX)"

DOTNET_VERSION="10.0.400"
DOTNET_SHA512="1033977dd837150e0814cf0c5d5b17ceb63925fda7ba2158b47258a4bd7c048cf82eac3bc1166f3146f53124a3f5fba09db1de1260d2ce96399860303b404b48"
POSTGRES_VERSION="18.6"
POSTGRES_SHA256="555610c24d53e4316da5b7d3fc25c279d96856d5e0e23ee308c328c5fa881d9f"
M4_VERSION="1.4.20"
M4_SHA256="e236ea3a1ccf5f6c270b1c4bb60726f371fa49459a8eaaebc90b216b328daf2b"
BISON_VERSION="3.8.2"
BISON_SHA256="9bba0214ccf7f1079c5d59210045227bcf619519840ebfa80cd3849cff5a5bf2"
FLEX_VERSION="2.6.4"
FLEX_SHA256="e87aae032bf07c26f85ac0ed3250998c37621d95f8bd748b31f15b33c45ee995"

cleanup() {
    case "$BUILD_ROOT" in
        /tmp/toi-no-mori-toolchain.*) rm -rf -- "$BUILD_ROOT" ;;
    esac
}
trap cleanup EXIT

for command_name in curl tar sha256sum sha512sum gcc make perl; do
    if ! command -v "$command_name" >/dev/null 2>&1; then
        echo "必要なbuild commandがありません: $command_name" >&2
        exit 2
    fi
done

mkdir -p "$TOOLS_ROOT" "$DOWNLOAD_ROOT"

download() {
    local url="$1"
    local destination="$2"
    if [[ ! -f "$destination" ]]; then
        curl --fail --show-error --location --retry 3 "$url" -o "$destination.part"
        mv -- "$destination.part" "$destination"
    fi
}

verify_sha256() {
    local expected="$1"
    local path="$2"
    local actual
    actual="$(sha256sum "$path" | awk '{print $1}')"
    if [[ "$actual" != "$expected" ]]; then
        echo "SHA-256不一致: $path" >&2
        exit 3
    fi
}

verify_sha512() {
    local expected="$1"
    local path="$2"
    local actual
    actual="$(sha512sum "$path" | awk '{print $1}')"
    if [[ "$actual" != "$expected" ]]; then
        echo "SHA-512不一致: $path" >&2
        exit 3
    fi
}

install_dotnet() {
    local archive="$DOWNLOAD_ROOT/dotnet-sdk-$DOTNET_VERSION-linux-x64.tar.gz"
    download \
        "https://builds.dotnet.microsoft.com/dotnet/Sdk/$DOTNET_VERSION/dotnet-sdk-$DOTNET_VERSION-linux-x64.tar.gz" \
        "$archive"
    verify_sha512 "$DOTNET_SHA512" "$archive"
    mkdir -p "$TOOLS_ROOT/dotnet"
    tar --no-same-owner -xzf "$archive" -C "$TOOLS_ROOT/dotnet"
}

build_gnu_tool() {
    local name="$1"
    local version="$2"
    local archive="$3"
    local extract_flag="$4"
    local expected_sha256="$5"
    local url="$6"
    download "$url" "$archive"
    verify_sha256 "$expected_sha256" "$archive"
    tar --no-same-owner "$extract_flag" "$archive" -C "$BUILD_ROOT"
    (
        cd "$BUILD_ROOT/$name-$version"
        ./configure --prefix="$TOOLS_ROOT/build-tools" --disable-nls
        make -j"${MVS01_BUILD_JOBS:-4}"
        make install
    )
}

install_postgresql() {
    mkdir -p "$TOOLS_ROOT/build-tools"
    export PATH="$TOOLS_ROOT/build-tools/bin:$PATH"

    if ! command -v m4 >/dev/null 2>&1; then
        build_gnu_tool \
            m4 "$M4_VERSION" "$DOWNLOAD_ROOT/m4-$M4_VERSION.tar.xz" -xJf "$M4_SHA256" \
            "https://ftp.gnu.org/gnu/m4/m4-$M4_VERSION.tar.xz"
    fi
    if ! command -v bison >/dev/null 2>&1; then
        build_gnu_tool \
            bison "$BISON_VERSION" "$DOWNLOAD_ROOT/bison-$BISON_VERSION.tar.xz" -xJf "$BISON_SHA256" \
            "https://ftp.gnu.org/gnu/bison/bison-$BISON_VERSION.tar.xz"
    fi
    if ! command -v flex >/dev/null 2>&1; then
        build_gnu_tool \
            flex "$FLEX_VERSION" "$DOWNLOAD_ROOT/flex-$FLEX_VERSION.tar.gz" -xzf "$FLEX_SHA256" \
            "https://github.com/westes/flex/releases/download/v$FLEX_VERSION/flex-$FLEX_VERSION.tar.gz"
    fi

    local archive="$DOWNLOAD_ROOT/postgresql-$POSTGRES_VERSION.tar.bz2"
    download \
        "https://ftp.postgresql.org/pub/source/v$POSTGRES_VERSION/postgresql-$POSTGRES_VERSION.tar.bz2" \
        "$archive"
    verify_sha256 "$POSTGRES_SHA256" "$archive"
    tar --no-same-owner -xjf "$archive" -C "$BUILD_ROOT"
    (
        cd "$BUILD_ROOT/postgresql-$POSTGRES_VERSION"
        ./configure \
            --prefix="$TOOLS_ROOT/postgresql" \
            --without-readline \
            --without-icu \
            --with-ssl=openssl \
            CFLAGS='-O2'
        make -j"${MVS01_BUILD_JOBS:-4}"
        make install
    )
}

if [[ ! -x "$TOOLS_ROOT/dotnet/dotnet" ]] \
    || [[ "$($TOOLS_ROOT/dotnet/dotnet --version 2>/dev/null || true)" != "$DOTNET_VERSION" ]]; then
    install_dotnet
fi

if [[ ! -x "$TOOLS_ROOT/postgresql/bin/postgres" ]] \
    || [[ "$($TOOLS_ROOT/postgresql/bin/postgres --version 2>/dev/null || true)" != "postgres (PostgreSQL) $POSTGRES_VERSION" ]]; then
    install_postgresql
fi

"$SCRIPT_DIR/verify-toolchain.sh"
