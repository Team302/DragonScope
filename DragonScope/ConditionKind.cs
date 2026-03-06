using System;

namespace DragonScope
{
    public enum ConditionKind { BoolTrue = 1, RangeOutOfBounds = 2, OpenEnded = 4 }

    public sealed class ParsedCondition
    {
        public string Name { get; init; } = "";
        public float Start { get; init; }
        public float? End { get; init; }
        public int Priority { get; init; }
        public ConditionKind Kind { get; init; }
        public string SourceFile { get; init; } = "";
    }
}