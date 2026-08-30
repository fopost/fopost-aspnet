using Xunit;

// Two tests read and restore FOPOST_API_KEY, which is process-wide state.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
