namespace ToiNoMori.Domain;

/// <summary>
/// Stage 6R のマルチテナント移行前データに割り当てる固定テナント。
/// API層で外部組織と内部テナントの対応付けを実装するまでの互換境界として使用する。
/// </summary>
public static class TenantIds
{
    public static readonly Guid Mvs01 = Guid.Parse("7b48e239-07ef-4b34-a1fb-7f4fc7ff1673");
}
