using Xunit;

// Integration tests use an in-process responder; run sequentially to avoid races.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
