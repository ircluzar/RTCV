namespace RTCV.UI
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Globalization;
    using System.IO;
    using System.Text.RegularExpressions;
    using System.Windows.Forms;
    using RTCV.CorruptCore;
    using RTCV.Common;
    using static RTCV.UI.UI_Extensions;

    public partial class RTC_ListGen_Form : ComponentForm, IAutoColorize, IBlockable
    {
        public new void HandleMouseDown(object s, MouseEventArgs e) => base.HandleMouseDown(s, e);
        public new void HandleFormClosing(object s, FormClosingEventArgs e) => base.HandleFormClosing(s, e);

        //long currentDomainSize = 0;

        public RTC_ListGen_Form()
        {
            InitializeComponent();
        }

        private ulong safeStringToULongHex(string input)
        {
            if (input.IndexOf("0X", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return ulong.Parse(input.Substring(2), NumberStyles.HexNumber);
            }
            else
            {
                return ulong.Parse(input, NumberStyles.HexNumber);
            }
        }

        private bool isHex(string str)
        {
            //Hex characters
            //Trim the 0x off
            string regex = "^((0[Xx])|([xX]))[0-9A-Fa-f]+$";
            return Regex.IsMatch(str, regex);
        }

        private bool isWholeNumber(string str)
        {
            string regex = "^[0-9]+$";
            return Regex.IsMatch(str, regex);
        }

        private bool isDecimalNumber(string str)
        {
            string regex = "^(\\d*\\.){1}\\d+$";
            return Regex.IsMatch(str, regex);
        }

        public static T Convert<T>(string input)
        {
            var converter = TypeDescriptor.GetConverter(typeof(T));
            if (converter != null)
            {
                //Cast ConvertFromString(string text) : object to (T)
                return (T)converter.ConvertFromString(input);
            }
            return default(T);
        }

        private void btnGenerateList_Click(object sender, EventArgs e)
        {
            GenerateList();
            tbListValues.Clear();
        }

        private bool GenerateList()
        {
            if (tbListValues.Lines.Length == 0)
            {
                return false;
            }
            List<string> newList = new List<string>();
            foreach (string line in tbListValues.Lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                string trimmedLine = line.Trim();

                string[] lineParts = trimmedLine.Split('-');

                //We can't do a range on anything besides plain old numbers
                if (lineParts.Length > 1)
                {
                    //Hex
                    if (isHex(lineParts[0]) && isHex(lineParts[1]))
                    {
                        ulong start = safeStringToULongHex(lineParts[0]);
                        ulong end = safeStringToULongHex(lineParts[1]);

                        for (ulong i = start; i < end; i++)
                        {
                            newList.Add(i.ToString("X"));
                        }
                    }
                    //Decimal
                    else if (isWholeNumber(lineParts[0]) && isWholeNumber(lineParts[1]))
                    {
                        ulong start = ulong.Parse(lineParts[0]);
                        ulong end = ulong.Parse(lineParts[1]);

                        for (ulong i = start; i < end; i++)
                        {
                            newList.Add(i.ToString("X"));
                        }
                    }
                }
                else
                {
                    //If it's not a range we parse for both prefixes and suffixes then see the type
                    if (isHex(trimmedLine)) //Hex with 0x prefix
                    {
                        newList.Add(lineParts[0].Substring(2));
                    }
                    else if (Regex.IsMatch(trimmedLine, "^[0-9]+[fF]$")) //123f float
                    {
                        float f = Convert<float>(trimmedLine.Substring(0, trimmedLine.Length - 1));
                        byte[] t = BitConverter.GetBytes(f);
                        newList.Add(CorruptCore_Extensions.BytesToHexString(t));
                    }
                    else if (Regex.IsMatch(trimmedLine, "^[0-9]+[dD]$")) //123d double
                    {
                        double d = Convert<double>(trimmedLine.Substring(0, trimmedLine.Length - 1));
                        byte[] t = BitConverter.GetBytes(d);
                        newList.Add(CorruptCore_Extensions.BytesToHexString(t));
                    }
                    else if (isDecimalNumber(trimmedLine)) //double no suffix
                    {
                        double d = Convert<double>(trimmedLine);
                        byte[] t = BitConverter.GetBytes(d);
                        newList.Add(CorruptCore_Extensions.BytesToHexString(t));
                    }
                    else if (isWholeNumber(trimmedLine)) //plain old number
                    {
                        newList.Add(ulong.Parse(trimmedLine).ToString("X"));
                    }
                }
            }

            string filename = CorruptCore_Extensions.MakeSafeFilename(tbListName.Text, '-');
            //Handle saving the list to a file
            if (cbSaveFile.Checked)
            {
                if (!string.IsNullOrWhiteSpace(filename))
                {
                    File.WriteAllLines(Path.Combine(CorruptCore.RtcCore.RtcDir, "LISTS", filename + ".txt"), newList);
                }
                else
                {
                    MessageBox.Show("Filename is empty. Unable to save your list to a file");
                }
            }

            //If there's no name just generate one
            if (string.IsNullOrWhiteSpace(filename))
            {
                filename = CorruptCore.RtcCore.GetRandomKey();
            }

            //TODO fix this before i forget

            IListFilter list;

            if (newList.Contains("?"))
            {
                list = new NullableByteArrayList();
            }
            else
            {
                list = new ValueByteArrayList();
            }

            list.Initialize(filename + ".txt", newList.ToArray(), false, false);
            Filtering.RegisterList(list, filename, true);
            var hash = list.GetHash();
            //Register the list and update netcore
            //List<byte?[]> byteList = new List<byte?[]>();
            //foreach (string t in newList)
            //{
            //    byte?[] bytes = CorruptCore_Extensions.StringToNullableByteArray(t);
            //    byteList.Add(bytes);
            //}
            //string hash = Filtering.RegisterList(byteList, filename, true);

            //Register the list in the ui
            CorruptCore.Filtering.RegisterListInUI(filename, hash);

            return true;
        }

        private void btnRefreshListsFromFile_Click(object sender, EventArgs e)
        {
            UICore.LoadLists(RtcCore.listsDir);
            UICore.LoadLists(Path.Combine(RtcCore.EmuDir, "LISTS"));
        }

        private void btnHelp_Click(object sender, EventArgs e)
        {
            MessageBox.Show(
@"List Generator instructions help and examples
-----------------------------------------------
A whole number will be treated as decimal.
A number prefixed with '0x' will be treated as hex.
You can use a range of these two types.

	A number with a decimal point will be treated as a double.
A number with the suffix 'd' will be treated as a double.
A number with the suffix 'f' will be treated as a float.


Examples:
8-11 -----> 8,9,A
0x8-0x11 -> 8,9,A,B,C,D,E,F,10
10 -------> A
0x10 -----> 10
1.0	------> 000000000000F03F
1d -------> 000000000000F03F
1f -------> 0000803F

> Ranges are exclusive, meaning that the last
	address is excluded from the range.");
        }
    }
}
