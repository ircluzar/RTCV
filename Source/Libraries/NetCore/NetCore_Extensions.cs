namespace RTCV.NetCore
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Reflection.Emit;
    using System.Runtime.ExceptionServices;
    using System.Runtime.InteropServices;
    using System.Runtime.Serialization.Formatters.Binary;
    using System.Text;
    using System.Threading;
    using System.Windows.Forms;
    using Ceras;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;

    public static class NetCore_Extensions
    {
        public static class ObjectCopier
        {
            public static T Clone<T>(T source)
            {
                if (!typeof(T).IsSerializable)
                {
                    throw new ArgumentException("The type must be serializable.", nameof(source));
                }

                //Return default of a null object
                if (object.ReferenceEquals(source, null))
                {
                    return default(T);
                }

                using (MemoryStream stream = new MemoryStream())
                {
                    BinaryFormatter formatter = new BinaryFormatter();
                    formatter.Serialize(stream, source);
                    stream.Seek(0, SeekOrigin.Begin);
                    return (T)formatter.Deserialize(stream);
                }
            }
        }

        public static class ConsoleHelper
        {
            public static ConsoleCopy con;

            public static void CreateConsole(string path = null)
            {
                ReleaseConsole();
                AllocConsole();
                if (!string.IsNullOrEmpty(path))
                {
                    con = new ConsoleCopy(path);
                }

                //Disable the X button on the console window
                EnableMenuItem(GetSystemMenu(GetConsoleWindow(), false), SC_CLOSE, MF_DISABLED);
            }

            private static bool ConsoleVisible = true;

            public static void ShowConsole()
            {
                var handle = GetConsoleWindow();
                ShowWindow(handle, SW_SHOW);
                ConsoleVisible = true;
            }

            public static void HideConsole()
            {
                var handle = GetConsoleWindow();
                ShowWindow(handle, SW_HIDE);
                ConsoleVisible = false;
            }

            public static void ToggleConsole()
            {
                if (ConsoleVisible)
                {
                    HideConsole();
                }
                else
                {
                    ShowConsole();
                }
            }

            public static void ReleaseConsole()
            {
                var handle = GetConsoleWindow();
                CloseHandle(handle);
            }
            // P/Invoke required:
            internal const int SW_HIDE = 0;
            internal const int SW_SHOW = 5;

            internal const int SC_CLOSE = 0xF060;           //close button's code in Windows API
            internal const int MF_ENABLED = 0x00000000;     //enabled button status
            internal const int MF_GRAYED = 0x1;             //disabled button status (enabled = false)
            internal const int MF_DISABLED = 0x00000002;    //disabled button status

            [DllImport("kernel32.dll")]
            private static extern IntPtr GetStdHandle(uint nStdHandle);

            [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
            public static extern bool CloseHandle(IntPtr handle);

            [DllImport("kernel32.dll")]
            private static extern void SetStdHandle(uint nStdHandle, IntPtr handle);

            [DllImport("kernel32")]
            private static extern bool AllocConsole();

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern IntPtr GetConsoleWindow();

            [DllImport("user32.dll")]
            public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

            [DllImport("user32.dll")]
            public static extern IntPtr GetSystemMenu(IntPtr HWNDValue, bool isRevert);

            [DllImport("user32.dll")]
            public static extern int EnableMenuItem(IntPtr tMenu, int targetItem, int targetStatus);

            public class ConsoleCopy : IDisposable
            {
                private FileStream fileStream;
                public StreamWriter FileWriter;
                private TextWriter doubleWriter;
                private TextWriter oldOut;

                private class DoubleWriter : TextWriter
                {
                    private TextWriter one;
                    private TextWriter two;

                    public DoubleWriter(TextWriter one, TextWriter two)
                    {
                        this.one = one;
                        this.two = two;
                    }

                    public override Encoding Encoding => one.Encoding;

                    public override void Flush()
                    {
                        one.Flush();
                        two.Flush();
                    }

                    public override void Write(char value)
                    {
                        one.Write(value);
                        two.Write(value);
                    }
                }

                public ConsoleCopy(string path)
                {
                    oldOut = Console.Out;

                    try
                    {
                        var dir = Path.GetDirectoryName(path);
                        if (!Directory.Exists(dir))
                        {
                            Directory.CreateDirectory(dir);
                        }

                        File.Create(path).Close();
                        fileStream = File.Open(path, FileMode.Open, FileAccess.Write, FileShare.Read);
                        FileWriter = new StreamWriter(fileStream)
                        {
                            AutoFlush = true
                        };

                        doubleWriter = new DoubleWriter(FileWriter, oldOut);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Cannot open file for writing");
                        Console.WriteLine(e.Message);
                        return;
                    }
                    Console.SetOut(doubleWriter);
                    Console.SetError(doubleWriter);
                }

                public void Dispose()
                {
                    Console.SetOut(oldOut);
                    if (FileWriter != null)
                    {
                        FileWriter.Flush();
                        FileWriter.Close();
                        FileWriter = null;
                    }
                    if (fileStream != null)
                    {
                        fileStream.Close();
                        fileStream = null;
                    }
                    if (doubleWriter != null)
                    {
                        doubleWriter.Dispose();
                        doubleWriter = null;
                    }
                }
            }
        }

        //Thanks to Riki, dev of Ceras for writing this
        public class HashSetFormatterThatKeepsItsComparer : Ceras.Formatters.IFormatter<HashSet<byte[]>>
        {
            // Sub-formatters are automatically set by Ceras' dependency injection
            public Ceras.Formatters.IFormatter<byte[]> _byteArrayFormatter;
            public Ceras.Formatters.IFormatter<IEqualityComparer<byte[]>> _comparerFormatter; // auto-implemented by Ceras using DynamicObjectFormatter

            public void Serialize(ref byte[] buffer, ref int offset, HashSet<byte[]> set)
            {
                // What do we need?
                // - The comparer
                // - Number of entries
                // - Actual content

                // Comparer
                _comparerFormatter.Serialize(ref buffer, ref offset, set.Comparer);

                // Count
                // We could use a 'IFormatter<int>' field, but Ceras will resolve it to this method anyway...
                SerializerBinary.WriteInt32(ref buffer, ref offset, set.Count);

                // Actual content
                foreach (var array in set)
                {
                    _byteArrayFormatter.Serialize(ref buffer, ref offset, array);
                }
            }

            public void Deserialize(byte[] buffer, ref int offset, ref HashSet<byte[]> set)
            {
                IEqualityComparer<byte[]> equalityComparer = null;
                _comparerFormatter.Deserialize(buffer, ref offset, ref equalityComparer);

                // We can already create the hashset
                set = new HashSet<byte[]>(equalityComparer);

                // Read content...
                int count = SerializerBinary.ReadInt32(buffer, ref offset);
                for (int i = 0; i < count; i++)
                {
                    byte[] ar = null;
                    _byteArrayFormatter.Deserialize(buffer, ref offset, ref ar);

                    set.Add(ar);
                }
            }
        }

        public class NullableByteHashSetFormatterThatKeepsItsComparer : Ceras.Formatters.IFormatter<HashSet<byte?[]>>
        {
            // Sub-formatters are automatically set by Ceras' dependency injection
            public Ceras.Formatters.IFormatter<byte?[]> _byteArrayFormatter;
            public Ceras.Formatters.IFormatter<IEqualityComparer<byte?[]>> _comparerFormatter; // auto-implemented by Ceras using DynamicObjectFormatter

            public void Serialize(ref byte[] buffer, ref int offset, HashSet<byte?[]> set)
            {
                // What do we need?
                // - The comparer
                // - Number of entries
                // - Actual content

                // Comparer
                _comparerFormatter.Serialize(ref buffer, ref offset, set.Comparer);

                // Count
                // We could use a 'IFormatter<int>' field, but Ceras will resolve it to this method anyway...
                SerializerBinary.WriteInt32(ref buffer, ref offset, set.Count);

                // Actual content
                foreach (var array in set)
                {
                    _byteArrayFormatter.Serialize(ref buffer, ref offset, array);
                }
            }

            public void Deserialize(byte[] buffer, ref int offset, ref HashSet<byte?[]> set)
            {
                IEqualityComparer<byte?[]> equalityComparer = null;
                _comparerFormatter.Deserialize(buffer, ref offset, ref equalityComparer);

                // We can already create the hashset
                set = new HashSet<byte?[]>(equalityComparer);

                // Read content...
                int count = SerializerBinary.ReadInt32(buffer, ref offset);
                for (int i = 0; i < count; i++)
                {
                    byte?[] ar = null;
                    _byteArrayFormatter.Deserialize(buffer, ref offset, ref ar);

                    set.Add(ar);
                }
            }
        }

        //https://stackoverflow.com/a/56931457
        public static object InvokeCorrectly(this Control control, Delegate method, params object[] args)
        {
            Exception failure = null;
            var result = control.Invoke(new Func<object>(() =>
            {
                try
                {
                    return method.DynamicInvoke(args);
                }
                catch (Exception ex)
                {
                    failure = ex.InnerException;
                    return failure;
                }
            }));
            if (failure != null)
            {
                ExceptionDispatchInfo.Capture(failure).Throw();
            }
            return result;
        }

        public static bool IsGDIEnhancedScalingAvailable() => (Environment.OSVersion.Version.Major == 10 &
                    Environment.OSVersion.Version.Build >= 17763);

        public enum DPI_AWARENESS
        {
            DPI_AWARENESS_INVALID = -1,
            DPI_AWARENESS_UNAWARE = 0,
            DPI_AWARENESS_SYSTEM_AWARE = 1,
            DPI_AWARENESS_PER_MONITOR_AWARE = 2
        }

        public enum DPI_AWARENESS_CONTEXT
        {
            DPI_AWARENESS_CONTEXT_DEFAULT = 0,
            DPI_AWARENESS_CONTEXT_UNAWARE = -1,
            DPI_AWARENESS_CONTEXT_SYSTEM_AWARE = -2,
            DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE = -3,
            DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = -4,
            DPI_AWARENESS_CONTEXT_UNAWARE_GDISCALED = -5
        }

        [DllImport("User32.dll")]
        public static extern DPI_AWARENESS_CONTEXT GetThreadDpiAwarenessContext();

        [DllImport("User32.dll")]
        public static extern DPI_AWARENESS_CONTEXT GetWindowDpiAwarenessContext(
            IntPtr hwnd);

        [DllImport("User32.dll")]
        public static extern DPI_AWARENESS_CONTEXT SetThreadDpiAwarenessContext(
            DPI_AWARENESS_CONTEXT dpiContext);
    }

    public static class SafeJsonTypeSerialization
    {
        public class JsonKnownTypesBinder : ISerializationBinder
        {
            public IList<Type> KnownTypes { get; set; }

            public JsonKnownTypesBinder()
            {
                KnownTypes = new List<Type>();
            }

            public Type BindToType(string assemblyName, string typeName) => KnownTypes.SingleOrDefault(t => t.Name == typeName);

            public void BindToName(Type serializedType, out string assemblyName, out string typeName)
            {
                assemblyName = null;
                typeName = serializedType.Name;
            }
        }

        //https://stackoverflow.com/a/38340375
        public class UntypedToTypedValueContractResolver : DefaultContractResolver
        {
            // As of 7.0.1, Json.NET suggests using a static instance for "stateless" contract resolvers, for performance reasons.
            // http://www.newtonsoft.com/json/help/html/ContractResolver.htm
            // http://www.newtonsoft.com/json/help/html/M_Newtonsoft_Json_Serialization_DefaultContractResolver__ctor_1.htm
            // "Use the parameterless constructor and cache instances of the contract resolver within your application for optimal performance."
            // See also https://stackoverflow.com/questions/33557737/does-json-net-cache-types-serialization-information
            private static UntypedToTypedValueContractResolver instance;

            // Explicit static constructor to tell C# compiler not to mark type as beforefieldinit
            static UntypedToTypedValueContractResolver() { instance = new UntypedToTypedValueContractResolver(); }

            public static UntypedToTypedValueContractResolver Instance => instance;

            protected override JsonDictionaryContract CreateDictionaryContract(Type objectType)
            {
                var contract = base.CreateDictionaryContract(objectType);

                if (contract.DictionaryValueType == typeof(object) && contract.ItemConverter == null)
                {
                    contract.ItemConverter = new UntypedToTypedValueConverter();
                }

                return contract;
            }
        }

        public class UntypedToTypedValueConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType) => throw new NotImplementedException("This converter should only be applied directly via ItemConverterType, not added to JsonSerializer.Converters");

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.Null)
                {
                    return null;
                }

                var value = serializer.Deserialize(reader, objectType);
                if (value is TypeWrapper)
                {
                    return ((TypeWrapper)value).ObjectValue;
                }
                return value;
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                if (serializer.TypeNameHandling == TypeNameHandling.None)
                {
                    Debug.WriteLine("ObjectItemConverter used when serializer.TypeNameHandling == TypeNameHandling.None");
                    serializer.Serialize(writer, value);
                }
                // Handle a couple of simple primitive cases where a type wrapper is not needed
                else if (value is string)
                {
                    writer.WriteValue((string)value);
                }
                else if (value is bool)
                {
                    writer.WriteValue((bool)value);
                }
                else
                {
                    var contract = serializer.ContractResolver.ResolveContract(value.GetType());
                    if (contract is JsonPrimitiveContract)
                    {
                        var wrapper = TypeWrapper.CreateWrapper(value);
                        serializer.Serialize(writer, wrapper, typeof(object));
                    }
                    else
                    {
                        serializer.Serialize(writer, value);
                    }
                }
            }
        }

        public abstract class TypeWrapper
        {
            protected TypeWrapper() { }

            [JsonIgnore]
            public abstract object ObjectValue { get; }

            public static TypeWrapper CreateWrapper<T>(T value)
            {
                if (value == null)
                {
                    return new TypeWrapper<T>();
                }

                var type = value.GetType();
                if (type == typeof(T))
                {
                    return new TypeWrapper<T>(value);
                }
                // Return actual type of subclass
                return (TypeWrapper)Activator.CreateInstance(typeof(TypeWrapper<>).MakeGenericType(type), value);
            }
        }

        public sealed class TypeWrapper<T> : TypeWrapper
        {
            public TypeWrapper() : base() { }

            public TypeWrapper(T value)
                : base()
            {
                this.Value = value;
            }

            public override object ObjectValue => Value;

            public T Value { get; set; }
        }
    }

    //https://stackoverflow.com/a/47744757/10923568
    public static class StackFrameHelper
    {
        private delegate object DGetStackFrameHelper();

        private static DGetStackFrameHelper _getStackFrameHelper = null;

        private static FieldInfo _frameCount = null;
        private static volatile bool initialized = false;

        public static void OneTimeSetup()
        {
            if (initialized)
            {
                return;
            }

            try
            {
                Type stackFrameHelperType =
                                 typeof(object).Assembly.GetType("System.Diagnostics.StackFrameHelper");

                // ReSharper disable once PossibleNullReferenceException
                MethodInfo getStackFramesInternal =
                   Type.GetType("System.Diagnostics.StackTrace, mscorlib").GetMethod(
                                "GetStackFramesInternal", BindingFlags.Static | BindingFlags.NonPublic);
                if (getStackFramesInternal == null)
                {
                    return;  // Unknown mscorlib implementation
                }

                DynamicMethod dynamicMethod = new DynamicMethod(
                          "GetStackFrameHelper", typeof(object), new Type[0], typeof(StackTrace), true);

                ILGenerator generator = dynamicMethod.GetILGenerator();
                generator.DeclareLocal(stackFrameHelperType);

                bool newDotNet = false;

                ConstructorInfo constructorInfo =
                         stackFrameHelperType.GetConstructor(new Type[] { typeof(bool), typeof(Thread) });
                if (constructorInfo != null)
                {
                    generator.Emit(OpCodes.Ldc_I4_0);
                }
                else
                {
                    constructorInfo = stackFrameHelperType.GetConstructor(new Type[] { typeof(Thread) });
                    if (constructorInfo == null)
                    {
                        return; // Unknown mscorlib implementation
                    }

                    newDotNet = true;
                }

                generator.Emit(OpCodes.Ldnull);
                generator.Emit(OpCodes.Newobj, constructorInfo);
                generator.Emit(OpCodes.Stloc_0);
                generator.Emit(OpCodes.Ldloc_0);
                generator.Emit(OpCodes.Ldc_I4_0);

                if (newDotNet)
                {
                    generator.Emit(OpCodes.Ldc_I4_0);  // Extra parameter
                }

                generator.Emit(OpCodes.Ldnull);
                generator.Emit(OpCodes.Call, getStackFramesInternal);
                generator.Emit(OpCodes.Ldloc_0);
                generator.Emit(OpCodes.Ret);

                _getStackFrameHelper =
                      (DGetStackFrameHelper)dynamicMethod.CreateDelegate(typeof(DGetStackFrameHelper));

                _frameCount = stackFrameHelperType.GetField("iFrameCount",
                                                        BindingFlags.NonPublic | BindingFlags.Instance);
                initialized = true;
            }
            catch
            { }  // _frameCount remains null, indicating unknown mscorlib implementation
        }

        public static int GetCallStackDepth()
        {
            if (!initialized)
            {
                OneTimeSetup();
            }

            if (_frameCount == null)
            {
                return 0;  // Unknown mscorlib implementation
            }

            return (int)_frameCount.GetValue(_getStackFrameHelper());
        }
    }

    internal sealed class TimePeriod : IDisposable
    {
        private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        private const string WINMM = "winmm.dll";

        private static TIMECAPS timeCapabilities;

        private static int inTimePeriod;

        private readonly int period;

        private int disposed;

        [DllImport(WINMM, ExactSpelling = true)]
        private static extern int timeGetDevCaps(ref TIMECAPS ptc, int cbtc);

        [DllImport(WINMM, ExactSpelling = true)]
        private static extern int timeBeginPeriod(int uPeriod);

        [DllImport(WINMM, ExactSpelling = true)]
        private static extern int timeEndPeriod(int uPeriod);

        static TimePeriod()
        {
            int result = timeGetDevCaps(ref timeCapabilities, Marshal.SizeOf(typeof(TIMECAPS)));
            if (result != 0)
            {
                logger.Error("The request to get time capabilities was not completed because an unexpected error with code {result} occured.", result);
            }
        }

        internal TimePeriod(int period)
        {
            if (Interlocked.Increment(ref inTimePeriod) != 1)
            {
                Interlocked.Decrement(ref inTimePeriod);
                //logger.Trace("The process is already within a time period. Nested time periods are not supported.");
                return;
            }

            if (period < timeCapabilities.wPeriodMin || period > timeCapabilities.wPeriodMax)
            {
                logger.Error("The request to begin a time period was not completed because the resolution specified is out of range.");
            }

            int result = timeBeginPeriod(period);
            if (result != 0)
            {
                logger.Error("The request to begin a time period was not completed because an unexpected error with code " + result + " occured.");
            }

            this.period = period;
        }

        internal static int MinimumPeriod => timeCapabilities.wPeriodMin;

        internal static int MaximumPeriod => timeCapabilities.wPeriodMax;

        internal int Period
        {
            get
            {
                if (this.disposed > 0)
                {
                    throw new ObjectDisposedException("The time period instance has been disposed.");
                }

                return this.period;
            }
        }

        public void Dispose()
        {
            if (Interlocked.Increment(ref this.disposed) == 1)
            {
                timeEndPeriod(this.period);
                Interlocked.Decrement(ref inTimePeriod);
            }
            else
            {
                Interlocked.Decrement(ref this.disposed);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct TIMECAPS
        {
            internal int wPeriodMin;

            internal int wPeriodMax;
        }
    }
}
