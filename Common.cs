using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ephemera.NStateMachine
{
    /// <summary>Definition for transition/entry/exit functions.</summary>
    /// <param name="o">Optional data.</param>
    public delegate void SmFunc(object? o);

    /// <summary>Data carrying class.</summary>
    public record EventInfo<S, E>(E EventId, object? Param);

    public static class Common<S, E>
    {
        /// <summary>Cast helper.</summary>
        public static S DEFAULT_STATE_ID = (S)(object)0;

        /// <summary>Cast helper.</summary>
        public static E DEFAULT_EVENT_ID = (E)(object)0;
    }
}
