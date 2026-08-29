# Review exchange

外部AIとの受け渡しを会話だけに残さず、Stage単位のreview packetとして保存します。

```text
docs/reviews/<stage>/
  review-manifest.json
  review-request.md
  claude-findings.md
  codex-response.md
  final-acceptance.md
```

- `review-request.md`: 読む対象と質問
- `claude-findings.md`: Claudeの原文Finding
- `codex-response.md`: Findingごとの技術応答
- `final-acceptance.md`: ユーザー判断。AIはPASSを書き込まない
- `review-manifest.json`: 機械可読な身元とstatus

最初のpacketは [stage6r11r](stage6r11r/review-request.md) です。
