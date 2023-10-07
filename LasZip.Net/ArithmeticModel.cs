// arithmeticmodel.{hpp, cpp}
using System;
using System.Diagnostics;

namespace LasZip
{
    internal class ArithmeticModel
    {
        private readonly bool compress;

        public UInt32[]? Distribution { get; set; }
        public UInt32[]? SymbolCount { get; set; }
        public UInt32[]? DecoderTable { get; set; }
        public UInt32 TotalCount { get; set; }
        public UInt32 UpdateCycle { get; set; }
        public UInt32 SymbolsUntilUpdate { get; set; }
        public UInt32 Symbols { get; set; }
        public UInt32 LastSymbol { get; set; }
        public UInt32 TableSize { get; set; }
        public int TableShift { get; set; }

        public ArithmeticModel(UInt32 symbols, bool compress)
        {
            this.Symbols = symbols;
            this.compress = compress;
            Distribution = null;
        }

        public int Init(UInt32[]? table = null)
        {
            if (this.Distribution == null)
            {
                if ((this.Symbols < 2) || (this.Symbols > (1 << 11)))
                    return -1; // invalid number of symbols

                this.LastSymbol = this.Symbols - 1;
                if ((!compress) && (this.Symbols > 16))
                {
                    int tableBits = 3;
                    while (Symbols > (1u << (tableBits + 2))) { tableBits++; }
                    this.TableSize = 1u << tableBits;
                    this.TableShift = GeneralModels.LengthShift - tableBits;

                    this.DecoderTable = new UInt32[TableSize + 2];
                }
                else // small alphabet: no table needed
                {
                    this.DecoderTable = null;
                    this.TableSize = 0;
                    this.TableShift = 0;
                }

                this.Distribution = new UInt32[this.Symbols];
                this.SymbolCount = new UInt32[this.Symbols];
            }
            Debug.Assert(this.SymbolCount != null);

            this.TotalCount = 0;
            this.UpdateCycle = this.Symbols;
            if (table != null)
            {
                for (UInt32 k = 0; k < Symbols; k++) 
                { 
                    this.SymbolCount[k] = table[k]; 
                }
            }
            else
            {
                for (UInt32 k = 0; k < Symbols; k++) 
                { 
                    this.SymbolCount[k] = 1; 
                }
            }

            Update();
            this.SymbolsUntilUpdate = this.UpdateCycle = (this.Symbols + 6) >> 1;

            return 0;
        }

        public void Update()
        {
            // halve counts when a threshold is reached
            if ((this.TotalCount += this.UpdateCycle) > GeneralModels.MaxCount)
            {
                this.TotalCount = 0;
                for (UInt32 n = 0; n < Symbols; n++)
                {
                    this.TotalCount += (this.SymbolCount[n] = (this.SymbolCount[n] + 1) >> 1);
                }
            }

            // compute cumulative distribution, decoder table
            UInt32 sum = 0, s = 0;
            UInt32 scale = 0x80000000u / TotalCount;

            if (compress || (TableSize == 0))
            {
                for (UInt32 k = 0; k < Symbols; k++)
                {
                    this.Distribution[k] = (scale * sum) >> (31 - GeneralModels.LengthShift);
                    sum += this.SymbolCount[k];
                }
            }
            else
            {
                for (UInt32 k = 0; k < Symbols; k++)
                {
                    this.Distribution[k] = (scale * sum) >> (31 - GeneralModels.LengthShift);
                    sum += this.SymbolCount[k];
                    UInt32 w = this.Distribution[k] >> TableShift;
                    while (s < w) { this.DecoderTable[++s] = k - 1; }
                }
                this.DecoderTable[0] = 0;
                while (s <= this.TableSize) { this.DecoderTable[++s] = this.Symbols - 1; }
            }

            // set frequency of model updates
            this.UpdateCycle = (5 * this.UpdateCycle) >> 2;
            UInt32 max_cycle = (this.Symbols + 6) << 3;
            if (this.UpdateCycle > max_cycle) { this.UpdateCycle = max_cycle; }
            this.SymbolsUntilUpdate = this.UpdateCycle;
        }
    }
}
