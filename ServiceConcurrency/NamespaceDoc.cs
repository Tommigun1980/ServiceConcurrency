namespace ServiceConcurrency
{
    /// <summary>
    /// A library for handling concurrency and state, primarily for code that
    /// calls out to web services.
    ///
    /// First, it prevents unnecessary calls from happening.When a call with
    /// matching arguments is already in flight, the concurrent caller is parked
    /// and will resume when the originating request finishes.If the argument
    /// is a collection, entities already in flight are stripped.
    /// 
    /// Second, it caches the state of value returning requests. When a cached
    /// value exists for any given request, it will be returned instead of a
    /// call being made. This value can be accessed at any time, preventing a
    /// need for additional backing fields in your code. If the argument
    /// is a collection, cached entities are stripped.
    /// 
    /// See https://github.com/Tommigun1980/ServiceConcurrency for examples and
    /// documentation.
    /// </summary>
    internal class NamespaceDoc
    {
    }
}
