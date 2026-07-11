// Integration tests boot the API in-process via WebApplicationFactory<Program>. Multiple such factories
// racing to invoke the top-level-statements entry point trip a HostFactoryResolver race
// ("entry point exited without ever building an IHost"). The suite is small and fast, so serialising it
// is the clean, idiomatic fix.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
