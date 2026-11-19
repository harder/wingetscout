# Unreleased

- Updated Markdig, filesystem abstractions, Microsoft.Extensions, xUnit, MTP,
  and the test-only async-interface dependency. Retained the Application Insights
  2.x compatibility bound; MTP 2.5.3 still uses an API removed in 3.x. See the
  [dependency compatibility review](../transitive-dependency-review.md).
- Made cancellation test startup deterministic and retained bounded CI failure
  diagnostics for managed tests and smoke checks.
