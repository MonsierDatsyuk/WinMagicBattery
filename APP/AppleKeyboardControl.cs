using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace MagicKeyboardMonitor
{
    public class AppleKeyboardControl
    {
        // Константы из Windows API
        private const uint GENERIC_WRITE = 0x40000000;
        private const uint FILE_SHARE_READ = 0x00000001;
        private const uint FILE_SHARE_WRITE = 0x00000002;
        private const uint OPEN_EXISTING = 3;
        private const uint FILE_ATTRIBUTE_NORMAL = 0x80;

        // Пути к различным устройствам Apple
        private const string DEVICE_PATH_MAGIC2 = @"\\.\AppleKeyMagic2Keyboard";
        private const string DEVICE_PATH_LEGACY = @"\\.\AppleKeyboard";

        // Код управления (IOCTL) – тот же, что и в C++ (0x0B403201C)
        private const uint IOCTL_SET_FN_MODE = 0x0B403201C;

        // Режимы Fn Lock
        public enum FnLockMode : uint
        {
            Enable = 0,   // Включить Fn lock (Fn работает как обычно)
            Disable = 1   // Отключить Fn lock (Fn работает как медиа-клавиши)
        }

        // Структура, соответствующая C++ struct MESSAGE
        [StructLayout(LayoutKind.Sequential)]
        private struct Message
        {
            public uint Code; // uint32_t в C++
        }

        // Импорт CreateFileA из kernel32.dll
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
        private static extern SafeFileHandle CreateFileA(
            string lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode,
            IntPtr lpSecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        // Импорт DeviceIoControl из kernel32.dll
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool DeviceIoControl(
            SafeFileHandle hDevice,
            uint dwIoControlCode,
            IntPtr lpInBuffer,
            uint nInBufferSize,
            IntPtr lpOutBuffer,
            uint nOutBufferSize,
            out uint lpBytesReturned,
            IntPtr lpOverlapped);

        /// <summary>
        /// Проверяет доступность устройства Apple Keyboard
        /// </summary>
        public static bool IsDeviceAvailable(string devicePath = null)
        {
            string path = devicePath ?? DEVICE_PATH_MAGIC2;

            try
            {
                using (SafeFileHandle hDevice = CreateFileA(
                    path,
                    GENERIC_WRITE,
                    FILE_SHARE_READ | FILE_SHARE_WRITE,
                    IntPtr.Zero,
                    OPEN_EXISTING,
                    FILE_ATTRIBUTE_NORMAL,
                    IntPtr.Zero))
                {
                    return hDevice != null && !hDevice.IsInvalid;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Установить режим Fn Lock с использованием перечисления
        /// </summary>
        public static bool SetFnLockMode(FnLockMode mode, bool useLegacyPath = false)
        {
            string devicePath = useLegacyPath ? DEVICE_PATH_LEGACY : DEVICE_PATH_MAGIC2;
            return SetFnLockMode((uint)mode, devicePath);
        }

        /// <summary>
        /// Отправляет команду устройству AppleKeyboard.
        /// </summary>
        /// <param name="codeValue">Значение, которое будет помещено в поле code структуры Message.</param>
        /// <returns>true в случае успеха, false при ошибке (подробности в исключении).</returns>
        /// <exception cref="Win32Exception">Выбрасывается при ошибке вызова WinAPI.</exception>
        public static bool SetFnLockMode(uint codeValue, string devicePath = null)
        {
            string path = devicePath ?? DEVICE_PATH_MAGIC2;

            // Открываем устройство
            using (SafeFileHandle hDevice = CreateFileA(
                path,
                GENERIC_WRITE,
                FILE_SHARE_READ | FILE_SHARE_WRITE,
                IntPtr.Zero,
                OPEN_EXISTING,
                FILE_ATTRIBUTE_NORMAL,
                IntPtr.Zero))
            {
                if (hDevice == null || hDevice.IsInvalid)
                {
                    int error = Marshal.GetLastWin32Error();
                    throw new Win32Exception(error, $"Не удалось открыть устройство {path}");
                }

                // Подготавливаем входной буфер (структуру Message)
                Message message = new Message { Code = codeValue };
                int structSize = Marshal.SizeOf<Message>();
                IntPtr lpInBuffer = Marshal.AllocHGlobal(structSize);

                try
                {
                    Marshal.StructureToPtr(message, lpInBuffer, false);

                    // Вызываем DeviceIoControl
                    uint bytesReturned;
                    bool success = DeviceIoControl(
                        hDevice,
                        IOCTL_SET_FN_MODE,
                        lpInBuffer,
                        (uint)structSize,
                        IntPtr.Zero,   // выходной буфер отсутствует
                        0,
                        out bytesReturned,
                        IntPtr.Zero);

                    if (!success)
                    {
                        int error = Marshal.GetLastWin32Error();
                        throw new Win32Exception(error, "Ошибка при вызове DeviceIoControl");
                    }

                    return true;
                }
                finally
                {
                    Marshal.FreeHGlobal(lpInBuffer);
                }
            }
        }
    }

    // Асинхронные расширения для AppleKeyboardControl
    public static class AppleKeyboardControlAsync
    {
        /// <summary>
        /// Асинхронно отправляет команду устройству AppleKeyboard.
        /// </summary>
        public static async Task<bool> SetFnLockModeAsync(uint codeValue, string devicePath = null)
        {
            return await Task.Run(() =>
            {
                try
                {
                    return AppleKeyboardControl.SetFnLockMode(codeValue, devicePath);
                }
                catch
                {
                    return false;
                }
            });
        }

        /// <summary>
        /// Асинхронно устанавливает режим Fn Lock
        /// </summary>
        public static async Task<bool> SetFnLockModeAsync(AppleKeyboardControl.FnLockMode mode, bool useLegacyPath = false)
        {
            return await Task.Run(() =>
            {
                try
                {
                    return AppleKeyboardControl.SetFnLockMode(mode, useLegacyPath);
                }
                catch
                {
                    return false;
                }
            });
        }
    }
}