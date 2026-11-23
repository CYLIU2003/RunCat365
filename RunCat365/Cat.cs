// Copyright 2025 Takuto Nakamura
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

namespace RunCat365
{
    internal abstract class Cat
    {
        private static readonly int[] Violation567 = [5, 6, 7];
        private static readonly int[] Violation56 = [5, 6];
        private static readonly int[] Violation5 = [5];
        private static readonly int[] Violation57 = [5, 7];
        private static readonly int[] Violation7 = [7];
        private static readonly int[] EmptyViolation = [];

        internal abstract ReadOnlySpan<int> ViolationIndices();
        internal abstract Cat Next();
        internal abstract string GetString();

        internal class Running : Cat
        {
            private static readonly Running[] Instances =
            [
                new Running(Frame.Frame0),
                new Running(Frame.Frame1),
                new Running(Frame.Frame2),
                new Running(Frame.Frame3),
                new Running(Frame.Frame4)
            ];

            private readonly string frameName;

            internal Frame CurrentFrame { get; }

            internal Running(Frame frame)
            {
                CurrentFrame = frame;
                frameName = $"running_{(int)frame}";
            }

            internal static Running FromFrame(Frame frame)
            {
                var index = (int)frame;
                return (uint)index < (uint)Instances.Length ? Instances[index] : Instances[0];
            }

            internal override ReadOnlySpan<int> ViolationIndices()
            {
                return CurrentFrame switch
                {
                    Frame.Frame0 => Violation567,
                    Frame.Frame1 => Violation56,
                    Frame.Frame2 => Violation56,
                    Frame.Frame3 => Violation5,
                    Frame.Frame4 => Violation57,
                    _ => EmptyViolation,
                };
            }

            internal override Cat Next()
            {
                return Instances[((int)CurrentFrame + 1) % Instances.Length];
            }

            internal override string GetString()
            {
                return frameName;
            }

            internal enum Frame
            {
                Frame0,
                Frame1,
                Frame2,
                Frame3,
                Frame4
            }
        }

        internal class Jumping : Cat
        {
            private static readonly Jumping[] Instances =
            [
                new Jumping(Frame.Frame0),
                new Jumping(Frame.Frame1),
                new Jumping(Frame.Frame2),
                new Jumping(Frame.Frame3),
                new Jumping(Frame.Frame4),
                new Jumping(Frame.Frame5),
                new Jumping(Frame.Frame6),
                new Jumping(Frame.Frame7),
                new Jumping(Frame.Frame8),
                new Jumping(Frame.Frame9)
            ];

            private readonly string frameName;

            internal Frame CurrentFrame { get; }

            internal Jumping(Frame frame)
            {
                CurrentFrame = frame;
                frameName = $"jumping_{(int)frame}";
            }

            internal static Jumping FromFrame(Frame frame)
            {
                var index = (int)frame;
                return (uint)index < (uint)Instances.Length ? Instances[index] : Instances[0];
            }

            internal override ReadOnlySpan<int> ViolationIndices()
            {
                return CurrentFrame switch
                {
                    Frame.Frame0 => Violation567,
                    Frame.Frame1 => Violation56,
                    Frame.Frame2 => Violation56,
                    Frame.Frame3 => Violation56,
                    Frame.Frame4 => Violation56,
                    Frame.Frame5 => Violation5,
                    Frame.Frame6 => EmptyViolation,
                    Frame.Frame7 => EmptyViolation,
                    Frame.Frame8 => EmptyViolation,
                    Frame.Frame9 => Violation7,
                    _ => EmptyViolation,
                };
            }

            internal override Cat Next()
            {
                return Instances[((int)CurrentFrame + 1) % Instances.Length];
            }

            internal override string GetString()
            {
                return frameName;
            }

            internal enum Frame
            {
                Frame0,
                Frame1,
                Frame2,
                Frame3,
                Frame4,
                Frame5,
                Frame6,
                Frame7,
                Frame8,
                Frame9
            }
        }
    }
}
