// arithmeticmodel.{hpp, cpp}
using System;

namespace LasZip
{
    internal class ArithmeticBitModel
    {
        public UInt32 UpdateCycle { get; set; }
        public UInt32 BitsUntilUpdate { get; set; }
        public UInt32 Bit0Prob { get; set; }
        public UInt32 Bit0Count { get; set; }
        public UInt32 BitCount { get; set; }

        public ArithmeticBitModel()
        {
            this.Init();
        }

        public int Init()
        {
            // initialization to equiprobable model
            Bit0Count = 1;
            BitCount = 2;
            Bit0Prob = 1u << (BinaryModels.LengthShift - 1);

            // start with frequent updates
            UpdateCycle = BitsUntilUpdate = 4;

            return 0;
        }

        public void Update()
        {
            // halve counts when a threshold is reached
            if ((BitCount += UpdateCycle) > BinaryModels.MaxCount)
            {
                BitCount = (BitCount + 1) >> 1;
                Bit0Count = (Bit0Count + 1) >> 1;
                if (Bit0Count == BitCount) ++BitCount;
            }

            // compute scaled bit 0 probability
            UInt32 scale = 0x80000000u / BitCount;
            Bit0Prob = (Bit0Count * scale) >> (31 - BinaryModels.LengthShift);

            // set frequency of model updates
            UpdateCycle = (5 * UpdateCycle) >> 2;
            if (UpdateCycle > 64) UpdateCycle = 64;
            BitsUntilUpdate = UpdateCycle;
        }
    }
}
