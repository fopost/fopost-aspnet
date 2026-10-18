# Changelog

## Unreleased

- `MapFoPostArticles` receives published blog articles from a website target: verifies
  the delivery, answers `article.test`, and hands `article.published` to an
  `IFoPostArticlePublishedHandler` or delegate registered with `AddFoPostArticleHandler`.
- `FoPost:Blog:WebhookSecret` option, `FoPostDefaults.ArticlesPath`,
  `FoPostDefaults.TimestampHeader`, `FoPostDefaults.TimestampedSignatureHeader`.
- Behaviour change: `MapFoPostWebhook` now verifies through `FoPostWebhooks.Verify` from
  `FoPost.Sdk`. A delivery carrying `X-FoPost-Timestamp` is checked against
  `X-FoPost-Signature-256` and refused when older than 300 seconds; a signed body that is
  not JSON is now `401` instead of `400`.
- Requires `FoPost.Sdk` 0.3.0, now resolved from NuGet; the parent-from-source build shim is gone.
