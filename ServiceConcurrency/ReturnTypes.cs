using System.Collections.Generic;

namespace ServiceConcurrency
{
#pragma warning disable CS1591
    // return types for execution methods that return state change info
    public struct StateInfo
    {
        public bool ChangedState { get; private set; }

        internal StateInfo(bool changedState)
        {
            this.ChangedState = changedState;
        }
    }

    public struct ResultAndStateInfo<TValue>
    {
        public TValue Result { get; private set; }
        public StateInfo StateInfo { get; private set; }

        internal ResultAndStateInfo(bool changedState, TValue result)
        {
            this.StateInfo = new StateInfo(changedState);
            this.Result = result;
        }
    }

    // return types for enumerable execution methods that return state change info
    public struct EnumerableStateInfo<TArg>
    {
        public IEnumerable<TArg> ChangedElements { get; private set; }

        internal EnumerableStateInfo(IEnumerable<TArg> changedElements)
        {
            this.ChangedElements = changedElements;
        }
    }

    public struct EnumerableResultAndStateInfo<TArg, TValue>
    {
        public IEnumerable<TValue> Result { get; private set; }
        public EnumerableStateInfo<TArg> EnumerableStateInfo { get; private set; }

        internal EnumerableResultAndStateInfo(IEnumerable<TArg> changedElements, IEnumerable<TValue> result)
        {
            this.EnumerableStateInfo = new EnumerableStateInfo<TArg>(changedElements);
            this.Result = result;
        }
    }
#pragma warning restore CS1591
}
