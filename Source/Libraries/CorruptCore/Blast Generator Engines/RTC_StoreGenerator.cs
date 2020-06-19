namespace RTCV.CorruptCore
{
    using System;

    public static class RTC_StoreGenerator
    {
        public static BlastLayer GenerateLayer(string note, string domain, long stepSize, long startAddress, long endAddress,
            ulong param1, ulong param2, int precision, int lifetime, int executeFrame, bool loop, int seed, BGStoreMode mode)
        {
            BlastLayer bl = new BlastLayer();

            Random rand = new Random(seed);
            //We subtract 1 at the end as precision is 1,2,4, and we need to go 0,1,3
            for (long address = startAddress; address < endAddress; address = address + stepSize + precision - 1)
            {
                BlastUnit bu = GenerateUnit(domain, address, param1, param2, stepSize, precision, lifetime, executeFrame, loop, mode, note, rand);
                if (bu != null)
                {
                    bl.Layer.Add(bu);
                }
            }

            return bl;
        }

        private static BlastUnit GenerateUnit(string domain, long address, ulong param1, ulong param2, long stepSize,
            int precision, int lifetime, int executeFrame, bool loop, BGStoreMode mode, string note, Random rand)
        {
            try
            {
                MemoryInterface mi = null;
                if (domain.Contains("[V]"))
                {
                    if (!MemoryDomains.VmdPool.ContainsKey(domain))
                    {
                        return null;
                    }

                    mi = MemoryDomains.VmdPool[domain];
                }
                else
                {
                    if (!MemoryDomains.MemoryInterfaces.ContainsKey(domain))
                    {
                        return null;
                    }

                    mi = MemoryDomains.MemoryInterfaces[domain];
                }


                byte[] value = new byte[precision];
                long destAddress = 0;
                StoreType storeType = StoreType.CONTINUOUS;


                if (address + value.Length > mi.Size)
                {
                    return null;
                }

                switch (mode)
                {
                    case BGStoreMode.CHAINED:
                        long temp = address + stepSize;
                        if (temp <= mi.Size)
                        {
                            destAddress = temp;
                        }
                        else
                        {
                            destAddress = mi.Size - 1;
                        }

                        break;
                    case BGStoreMode.SOURCE_RANDOM:
                        destAddress = address;
                        address = rand.Next(0, Convert.ToInt32(mi.Size - 1));
                        break;
                    case BGStoreMode.SOURCE_SET:
                        destAddress = address;
                        address = (long)param1;
                        break;
                    case BGStoreMode.DEST_RANDOM:
                        destAddress = rand.Next(0, Convert.ToInt32(mi.Size - 1));
                        break;
                    case BGStoreMode.SELF:
                        destAddress = address;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
                }

                if (destAddress >= mi.Size)
                {
                    return null;
                }

                var bu = new BlastUnit(storeType, StoreTime.PREEXECUTE, domain, destAddress, domain, address, precision, mi.BigEndian, executeFrame, lifetime, note)
                {
                    Loop = loop
                };
                return bu;
            }
            catch (Exception ex)
            {
                throw new NetCore.CustomException("Something went wrong in the RTC StoreGenerator Generator. " + ex.Message, ex.StackTrace);
            }
        }
    }
}
