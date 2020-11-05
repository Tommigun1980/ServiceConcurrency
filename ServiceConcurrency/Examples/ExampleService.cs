using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

namespace ServiceConcurrency
{
    internal class Example
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
        // fetch the value from cache.Also prevents any concurrent calls from occurring,
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
        // in one call with ["A", "B", "C"] and one with ["D"]).
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
        // or an operation is already in flight.
        //
        // So simultaneously calling with ["A", "B", "C"]) and ["B", "C", "D"] will
        // result in one call with ["A", "B", "C"] and one with ["D"]).
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

}
