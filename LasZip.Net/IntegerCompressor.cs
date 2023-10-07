// integercompressor.{hpp, cpp}
using System;
using System.Diagnostics;

namespace LasZip
{
    internal class IntegerCompressor
    {
        private UInt32 k;

        private UInt32 contexts;
        private UInt32 bits_high;

        private UInt32 bits;
        private UInt32 range;

        private UInt32 corr_bits;
        private UInt32 corr_range;
        private int corr_min;
        private int corr_max;

        private readonly ArithmeticEncoder? enc;
        private readonly ArithmeticDecoder? dec;

        private ArithmeticModel[]? mBits;
        private ArithmeticModel[]? mCorrector; // mCorrector[0] will always be null
        private ArithmeticBitModel? mCorrectorBit;

        public IntegerCompressor(ArithmeticEncoder enc, UInt32 bits = 16, UInt32 contexts = 1, UInt32 bits_high = 8, UInt32 range = 0)
        {
            Debug.Assert(enc != null);
            this.enc = enc;
            this.dec = null;

            Init(bits, contexts, bits_high, range);
        }

        public IntegerCompressor(ArithmeticDecoder dec, UInt32 bits = 16, UInt32 contexts = 1, UInt32 bits_high = 8, UInt32 range = 0)
        {
            Debug.Assert(dec != null);
            this.enc = null;
            this.dec = dec;

            Init(bits, contexts, bits_high, range);
        }

        // Get the k corrector bits from the last compress/decompress call
        public UInt32 GetK() { return k; }

        void Init(UInt32 bits = 16, UInt32 contexts = 1, UInt32 bits_high = 8, UInt32 range = 0)
        {
            this.bits = bits;
            this.contexts = contexts;
            this.bits_high = bits_high;
            this.range = range;

            if (range != 0) // the corrector's significant bits and range
            {
                corr_bits = 0;
                corr_range = range;
                while (range != 0)
                {
                    range = range >> 1;
                    corr_bits++;
                }
                if (corr_range == (1u << ((int)corr_bits - 1)))
                {
                    corr_bits--;
                }
                // the corrector must fall into this interval
                corr_min = -((int)(corr_range / 2));
                corr_max = (int)(corr_min + corr_range - 1);
            }
            else if (bits != 0 && bits < 32)
            {
                corr_bits = bits;
                corr_range = 1u << (int)bits;
                // the corrector must fall into this interval
                corr_min = -((int)(corr_range / 2));
                corr_max = (int)(corr_min + corr_range - 1);
            }
            else
            {
                corr_bits = 32;
                corr_range = 0;
                // the corrector must fall into this interval
                corr_min = int.MinValue;
                corr_max = int.MaxValue;
            }

            k = 0;

            mBits = null;
            mCorrector = null;
        }

        // Manage Compressor
        public void InitCompressor()
        {
            Debug.Assert(enc != null);

            // maybe create the models
            if (mBits == null)
            {
                mBits = new ArithmeticModel[contexts];
                for (UInt32 i = 0; i < contexts; i++)
                {
                    mBits[i] = ArithmeticEncoder.CreateSymbolModel(corr_bits + 1);
                }
#if !COMPRESS_ONLY_K
                mCorrector = new ArithmeticModel[corr_bits + 1];
                mCorrectorBit = ArithmeticEncoder.CreateBitModel();
                for (UInt32 i = 1; i <= corr_bits; i++)
                {
                    if (i <= bits_high)
                    {
                        mCorrector[i] = ArithmeticEncoder.CreateSymbolModel(1u << (int)i);
                    }
                    else
                    {
                        mCorrector[i] = ArithmeticEncoder.CreateSymbolModel(1u << (int)bits_high);
                    }
                }
#endif
            }

            // certainly init the models
            for (UInt32 i = 0; i < contexts; i++)
            {
                ArithmeticEncoder.InitSymbolModel(mBits[i]);
            }

#if !COMPRESS_ONLY_K
            ArithmeticEncoder.InitBitModel(mCorrectorBit);
            for (UInt32 i = 1; i <= corr_bits; i++)
            {
                ArithmeticEncoder.InitSymbolModel(mCorrector[i]);
            }
#endif
        }

        public void Compress(int pred, int real, UInt32 context = 0)
        {
            if (this.mBits == null)
            {
                throw new InvalidOperationException();
            }

            // the corrector will be within the interval [ - (corr_range - 1)  ...  + (corr_range - 1) ]
            int corr = real - pred;

            // we fold the corrector into the interval [ corr_min  ...  corr_max ]
            if (corr < corr_min) corr += (int)corr_range;
            else if (corr > corr_max) corr -= (int)corr_range;
            WriteCorrector(corr, mBits[context]);
        }

        // Manage Decompressor
        public void InitDecompressor()
        {
            Debug.Assert(dec != null);

            // maybe create the models
            if (mBits == null)
            {
                mBits = new ArithmeticModel[contexts];
                for (UInt32 i = 0; i < contexts; i++)
                {
                    mBits[i] = ArithmeticDecoder.CreateSymbolModel(corr_bits + 1);
                }

#if !COMPRESS_ONLY_K
                mCorrector = new ArithmeticModel[corr_bits + 1];
                mCorrectorBit = ArithmeticDecoder.CreateBitModel();
                for (UInt32 i = 1; i <= corr_bits; i++)
                {
                    if (i <= bits_high)
                    {
                        mCorrector[i] = ArithmeticDecoder.CreateSymbolModel(1u << (int)i);
                    }
                    else
                    {
                        mCorrector[i] = ArithmeticDecoder.CreateSymbolModel(1u << (int)bits_high);
                    }
                }
#endif
            }

            // certainly init the models
            for (UInt32 i = 0; i < contexts; i++)
            {
                ArithmeticDecoder.InitSymbolModel(mBits[i]);
            }

#if !COMPRESS_ONLY_K
            ArithmeticDecoder.InitBitModel(mCorrectorBit);
            for (UInt32 i = 1; i <= corr_bits; i++)
            {
                ArithmeticDecoder.InitSymbolModel(mCorrector[i]);
            }
#endif
        }

        public int Decompress(int pred, UInt32 context = 0)
        {
            if (this.mBits == null)
            {
                throw new InvalidOperationException();
            }

            int real = pred + ReadCorrector(this.mBits[context]);
            if (real < 0) real += (int)corr_range;
            else if ((UInt32)(real) >= corr_range) real -= (int)corr_range;
            return real;
        }

        private void WriteCorrector(int c, ArithmeticModel model)
        {
            // find the tighest interval [ - (2^k - 1)  ...  + (2^k) ] that contains c
            k = 0;

            // do this by checking the absolute value of c (adjusted for the case that c is 2^k)
            UInt32 c1 = (UInt32)(c <= 0 ? -c : c - 1);

            // this loop could be replaced with more efficient code
            while (c1 != 0)
            {
                c1 = c1 >> 1;
                k = k + 1;
            }

            // the number k is between 0 and corr_bits and describes the interval the corrector falls into
            // we can compress the exact location of c within this interval using k bits
            enc.EncodeSymbol(model, k);

#if COMPRESS_ONLY_K
			if(k!=0) // then c is either smaller than 0 or bigger than 1
			{
				Debug.Assert((c!=0)&&(c!=1));
				if(k<32)
				{
					// translate the corrector c into the k-bit interval [ 0 ... 2^k - 1 ]
					if(c<0) // then c is in the interval [ - (2^k - 1)  ...  - (2^(k-1)) ]
					{
						// so we translate c into the interval [ 0 ...  + 2^(k-1) - 1 ] by adding (2^k - 1)
						enc.writeBits((int)k, (UInt32)(c+((1<<(int)k)-1)));
					}
					else // then c is in the interval [ 2^(k-1) + 1  ...  2^k ]
					{
						// so we translate c into the interval [ 2^(k-1) ...  + 2^k - 1 ] by subtracting 1
						enc.writeBits((int)k, (UInt32)(c-1));
					}
				}
			}
			else // then c is 0 or 1
			{
				Debug.Assert((c==0)||(c==1));
				enc.writeBit((UInt32)c);
			}
#else // COMPRESS_ONLY_K
            if (k != 0) // then c is either smaller than 0 or bigger than 1
            {
                Debug.Assert((c != 0) && (c != 1));
                if (k < 32)
                {
                    // translate the corrector c into the k-bit interval [ 0 ... 2^k - 1 ]
                    if (c < 0) // then c is in the interval [ - (2^k - 1)  ...  - (2^(k-1)) ]
                    {
                        // so we translate c into the interval [ 0 ...  + 2^(k-1) - 1 ] by adding (2^k - 1)
                        c += ((1 << (int)k) - 1);
                    }
                    else // then c is in the interval [ 2^(k-1) + 1  ...  2^k ]
                    {
                        // so we translate c into the interval [ 2^(k-1) ...  + 2^k - 1 ] by subtracting 1
                        c -= 1;
                    }
                    if (k <= bits_high) // for small k we code the interval in one step
                    {
                        // compress c with the range coder
                        enc.EncodeSymbol(mCorrector[k], (UInt32)c);
                    }
                    else // for larger k we need to code the interval in two steps
                    {
                        // figure out how many lower bits there are
                        int k1 = (int)k - (int)bits_high;
                        // c1 represents the lowest k-bits_high+1 bits
                        c1 = (UInt32)(c & ((1 << k1) - 1));
                        // c represents the highest bits_high bits
                        c = c >> k1;
                        // compress the higher bits using a context table
                        enc.EncodeSymbol(mCorrector[k], (UInt32)c);
                        // store the lower k1 bits raw
                        enc.WriteBits(k1, c1);
                    }
                }
            }
            else // then c is 0 or 1
            {
                Debug.Assert((c == 0) || (c == 1));
                enc.EncodeBit(mCorrectorBit, (UInt32)c);
            }
#endif // COMPRESS_ONLY_K
        }

        private int ReadCorrector(ArithmeticModel model)
        {
            Debug.Assert(this.dec != null);
            int c;

            // decode within which interval the corrector is falling
            this.k = this.dec.DecodeSymbol(model);

            // decode the exact location of the corrector within the interval

#if COMPRESS_ONLY_K
			if(k!=0) // then c is either smaller than 0 or bigger than 1
			{
				if(k<32)
				{
					c=(int)dec.readBits(k);

					if(c>=(1<<((int)k-1))) // if c is in the interval [ 2^(k-1)  ...  + 2^k - 1 ]
					{
						// so we translate c back into the interval [ 2^(k-1) + 1  ...  2^k ] by adding 1 
						c+=1;
					}
					else // otherwise c is in the interval [ 0 ...  + 2^(k-1) - 1 ]
					{
						// so we translate c back into the interval [ - (2^k - 1)  ...  - (2^(k-1)) ] by subtracting (2^k - 1)
						c-=((1<<(int)k)-1);
					}
				}
				else
				{
					c=corr_min;
				}
			}
			else // then c is either 0 or 1
			{
				c=(int)dec.readBit();
			}
#else // COMPRESS_ONLY_K
            if (k != 0) // then c is either smaller than 0 or bigger than 1
            {
                if (k < 32)
                {
                    if (k <= bits_high) // for small k we can do this in one step
                    {
                        // decompress c with the range coder
                        c = (int)dec.DecodeSymbol(mCorrector[k]);
                    }
                    else
                    {
                        // for larger k we need to do this in two steps
                        UInt32 k1 = k - bits_high;
                        // decompress higher bits with table
                        c = (int)dec.DecodeSymbol(mCorrector[k]);
                        // read lower bits raw
                        int c1 = (int)dec.ReadBits(k1);
                        // put the corrector back together
                        c = (c << (int)k1) | c1;
                    }
                    // translate c back into its correct interval
                    if (c >= (1 << ((int)k - 1))) // if c is in the interval [ 2^(k-1)  ...  + 2^k - 1 ]
                    {
                        // so we translate c back into the interval [ 2^(k-1) + 1  ...  2^k ] by adding 1 
                        c += 1;
                    }
                    else // otherwise c is in the interval [ 0 ...  + 2^(k-1) - 1 ]
                    {
                        // so we translate c back into the interval [ - (2^k - 1)  ...  - (2^(k-1)) ] by subtracting (2^k - 1)
                        c -= ((1 << (int)k) - 1);
                    }
                }
                else
                {
                    c = corr_min;
                }
            }
            else // then c is either 0 or 1
            {
                c = (int)dec.DecodeBit(mCorrectorBit);
            }
#endif // COMPRESS_ONLY_K

            return c;
        }
    }
}
