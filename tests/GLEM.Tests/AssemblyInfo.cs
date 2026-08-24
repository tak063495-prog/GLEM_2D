using Xunit;

// Culture-mutating tests (CultureScope) change thread/process-wide culture state that resource lookup and
// string formatting depend on. Serialize test collections so no two tests can observe each other's cultures.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
