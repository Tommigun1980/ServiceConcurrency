# ServiceConcurrency
_A concurrency and state library for .NET, shines in net call services._

NuGet package available at https://www.nuget.org/packages/ServiceConcurrency/

## Intro
A library for handling concurrency and state, primarily for code that
calls out to web services.

First, it prevents unnecessary calls from happening. When a call with
matching arguments is already in flight, the concurrent caller is parked
and will resume when the originating request finishes. If the argument
is a collection, entities already in flight are stripped.

Second, it caches the state of value returning requests. When a cached
value exists for any given request, it will be returned instead of a
call being made. This value can be accessed at any time, preventing a
need for additional backing fields in your code. If the argument is a
collection, cached entities are stripped from the argument.


## Real-life examples

Here's a few things ServiceConcurrency can handle for you. More in-depth examples
can be found in later sections of this document.

GetSessionToken() will return a cached value after a first call has been made.
If GetSessionToken() is called concurrently while it doesn't yet have a value
cached, only one call to FetchSessionToken() will ever be made and the other
concurrent calls will yield until a value is available.

```c#
private ServiceConcurrency.ReturnsValue<string> sessionTokenState =
    new ServiceConcurrency.ReturnsValue<string>();

public async Task<string> GetSessionToken()
{
    return await this.sessionTokenState.Execute(this.FetchSessionToken);
}
```

Same as above, but the argument collection is stripped of values that are
already cached or in flight. So FetchUserProfiles() will never be called with
an id more than once.

```c#
private ServiceConcurrency.TakesEnumerationArgReturnsValue<Guid, UserProfile> userProfilesState =
    new ServiceConcurrency.TakesEnumerationArgReturnsValue<Guid, UserProfile>();

public async Task<IEnumerable<UserProfile>> GetUserProfiles(IEnumerable<Guid> userProfileIds)
{
    return await this.userProfilesState.Execute(
        this.FetchUserProfiles,
        (guid, results) => results.SingleOrDefault(t => t.Id == guid),
        userProfileIds
    );
}
```

## Classes
There are four classes in this library:

**NoArgNoValue**

Prevents concurrent calls by allowing only one active call at a time,
where concurrent calls will wait for the active call to finish.

**ReturnsValue&lt;TValue&gt;**

Only one call will be made and subsequent calls will fetch the value from
cache. Also prevents any concurrent calls from occurring, by allowing only
one active call at a time, where concurrent calls will wait for the active
call to finish.

**TakesArg&lt;TArg&gt;**

Prevents concurrent calls when sharing the same argument, by allowing only
one active call at a time per argument, where concurrent calls will wait for
the active call to finish.

**TakesArgReturnsValue&lt;TArg, TValue&gt;**

For a given argument, only one call will be made and subsequent calls will
fetch the value from cache. Also prevents any concurrent calls from occurring,
by allowing only one active call at a time per argument, where concurrent
calls will wait for the active call to finish.

**TakesEnumerationArg&lt;TArg&gt;**

Concurrent calls will execute only once for a given argument in the argument
collection.

The argument collection is stripped of values for which an operation is already
in flight.

So simultaneously calling with ["A", "B", "C"] and ["B", "C", "D"] will result
in one call with ["A", "B", "C"] and one with ["D"].

**TakesEnumerationArgReturnsValue&lt;TArg, TValue&gt;**

The first call, and any concurrent calls, will execute only once for a
given argument in the argument collection. Subsequent calls will fetch value
from cache.

The argument collection is stripped of values for which a cached value exists
or an operation is already in flight.

So simultaneously calling with ["A", "B", "C"] and ["B", "C", "D"] will
result in one call with ["A", "B", "C"] and one with ["D"].
The next time "A", "B", "C" or "D" is called with, it will be stripped from
the collection and a value for it will be fetched from the cache.

## API
### Methods

```c#
T Execute(...)
```

Executes a specific request. You provide a callback for making the outside call. See the examples for more information, as the arguments and return types are ServiceConcurrency type specific.

```c#
void Reset()
```

Resets the internal state, ie state for calls in process and internal cache (in case the ServiceConcurrency object returns values).

```c#
bool IsExecuting()
```

Returns whether the ServiceConcurrency object is currently executing a specific request.

```c#
void ResetCache()
```

Only in ServiceConcurrency objects that return values. Resets the internal cache. Also called from Reset().

### Properties

```c#
bool EnableCache;
```

Only in ServiceConcurrency objects that return values. If turned off, the cache will be bypassed.

```c#
TValue Value;
```

Only in ReturnsValue&lt;TValue&gt;. This is the cached state.

```c#
Dictionary<TArg, TValue> ValueMap;
```

Only in TakesArgReturnsValue&lt;TArg, TValue&gt; and TakesEnumerationArgReturnsValue&lt;TArg, TValue&gt;. This is the cached state, per argument.

### Value conversion (optional)

Execute() accepts an optional value converter, which can modify the fetched value before returning and caching it. This is available only in the ServiceConcurrency objects that return values.

```c#
private ServiceConcurrency.ReturnsValue<string> lastName =
    new ServiceConcurrency.ReturnsValue<string>();

private const string FirstName = "John";

public async Task<string> GetFullName()
{
    return await this.lastName.Execute(
        this.GetLastName,
        (lastName) => $"{FirstName} {lastName}";
    );
}
```

ServiceConcurrency objects also accept an additional parameter that declare the value type of the internal request. When this is specified, the value converter will also convert between the source type and the destination type. This is useful if the request invoked by Execute() is of a different type than the desired backing field.

```c#
// FetchChatRooms() returns an IEnumerable<ChatRoom>, chatRoomMap handles it as Dictionary<Guid, ChatRoom>
private ServiceConcurrency.ReturnsValue<IEnumerable<ChatRoom>, Dictionary<Guid, ChatRoom>> chatRoomMap =
    new ServiceConcurrency.ReturnsValue<IEnumerable<ChatRoom>, Dictionary<Guid, ChatRoom>>();

public async Task<IEnumerable<ChatRoom>> UpdateChatRooms()
{
    return (await this.chatRoomMap.Execute(
        this.FetchChatRooms,
        (chatRooms) => chatRooms.ToDictionary(t => t.Id, t => t) // cache as id -> chatroom map
    ))?.Values;
}

public ChatRoom GetChatRoom(Guid chatRoomId)
{
    ChatRoom chatRoom;
    if (this.chatRoomMap.Value.TryGetValue(chatRoomId, out chatRoom)) // value is Dictionary<Guid, ChatRoom>
        return chatRoom;
    return null;
}
```

## Examples ##

```c#
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

public class MyService
{
    ////////////////////////////////////////////////////////////////////////////
    // NoArgNoValue example
    private ServiceConcurrency.NoArgNoValue simpleCallState =
        new ServiceConcurrency.NoArgNoValue();

    // Concurrent calls won't invoke the callback multiple times - only the first
    // call will invoke it, and the rest will wait until it finishes.
    public async Task CallSomething()
    {
        await this.simpleCallState.Execute(
            async () =>
            {
                Console.WriteLine("CallSomething call in flight");
                await Task.Delay(100);
            }
        );
    }


    ////////////////////////////////////////////////////////////////////////////
    // ReturnsValue example
    private ServiceConcurrency.ReturnsValue<string> returnsValueState =
        new ServiceConcurrency.ReturnsValue<string>();

    // Only one call will be made and subsequent calls will fetch the value from
    // cache. Also prevents any concurrent calls from occurring, by allowing only
    // one active call at a time, where concurrent calls will wait for the active
    // call to finish.
    public async Task<string> FetchSomething()
    {
        return await this.returnsValueState.Execute(
            async () =>
            {
                Console.WriteLine("FetchSomething call in flight");
                await Task.Delay(100);
                return "Hello world!";
            }
        );
    }


    ////////////////////////////////////////////////////////////////////////////
    // TakesArg example
    private ServiceConcurrency.TakesArg<Guid> takesArgState =
        new ServiceConcurrency.TakesArg<Guid>();

    // Prevents concurrent calls when sharing the same argument, by allowing only
    // one active call at a time per argument, where concurrent calls will wait for
    // the active call to finish.
    public async Task PostSomething(Guid someId)
    {
        await this.takesArgState.Execute(
            async (Guid id) =>
            {
                Console.WriteLine($"PostSomething call in flight, for argument {id}");
                await Task.Delay(100);
            },
            someId
        );
    }


    ////////////////////////////////////////////////////////////////////////////
    // TakesArgReturnsValue example
    private ServiceConcurrency.TakesArgReturnsValue<Guid, string> takesArgReturnsValueState =
        new ServiceConcurrency.TakesArgReturnsValue<Guid, string>();

    // For a given argument, only one call will be made and subsequent calls will
    // fetch the value from cache. Also prevents any concurrent calls from occurring,
    // by allowing only one active call at a time per argument, where concurrent
    // calls will wait for the active call to finish.
    public async Task<string> FetchSomethingFor(Guid someId)
    {
        return await this.takesArgReturnsValueState.Execute(
            async (Guid id) =>
            {
                Console.WriteLine($"FetchSomethingFor call in flight, for argument {id}");
                await Task.Delay(100);
                return $"The guid is {id}";
            },
            someId
        );
    }


    ////////////////////////////////////////////////////////////////////////////
    // TakesEnumerationArg example
    private ServiceConcurrency.TakesEnumerationArg<Guid> takesEnumerationArgState =
        new ServiceConcurrency.TakesEnumerationArg<Guid>();

    // Concurrent calls will execute only once for a given argument in the argument
    // collection.
    // 
    // The argument collection is stripped of values for which an operation is already
    // in flight.
    //
    // So simultaneously calling with ["A", "B", "C"] and ["B", "C", "D"] will result
    // in one call with ["A", "B", "C"] and one with ["D"].
    public async Task PostCollection(IEnumerable<Guid> someIds)
    {
        await this.takesEnumerationArgState.Execute(
            async (IEnumerable<Guid> ids) =>
            {
                Console.WriteLine($"PostCollection call in flight, for arguments {ids.Select(t => t)}");
                await Task.Delay(100);
            },
            someIds
        );
    }


    ////////////////////////////////////////////////////////////////////////////
    // TakesEnumerationArgReturnsValue example
    private ServiceConcurrency.TakesEnumerationArgReturnsValue<Guid, ExampleClass> takesEnumArgReturnsValueState =
        new ServiceConcurrency.TakesEnumerationArgReturnsValue<Guid, ExampleClass>();

    public class ExampleClass
    {
        public Guid Id { get; set; }
    }

    // The first call, and any concurrent calls, will execute only once for a
    // given argument in the argument collection. Subsequent calls will fetch value
    // from cache.
    // 
    // The argument collection is stripped of values for which a cached value exists
    // or an operation is alraedy in flight.
    //
    // So simultaneously calling with ["A", "B", "C"] and ["B", "C", "D"] will
    // result in one call with ["A", "B", "C"] and one with ["D"].
    // The next time "A", "B", "C" or "D" is called with, it will be stripped from
    // the collection and a value for it will be fetched from the cache.
    public async Task<IEnumerable<ExampleClass>> FetchCollectionForThese(IEnumerable<Guid> someIds)
    {
        return await this.takesEnumArgReturnsValueState.Execute(
            async (IEnumerable<Guid> ids) =>
            {
                Console.WriteLine($"FetchCollectionForThese call in flight, for arguments {ids.Select(t => t)}");
                await Task.Delay(100);

                return ids.Select(t => new ExampleClass()
                {
                    Id = t
                });
            },

            // a mapper from arg to result is required - should return the corresponding value for the passed argument
            (Guid id, IEnumerable<ExampleClass> result) => result.SingleOrDefault(t => t.Id == id),

            someIds
        );
    }
}
```
