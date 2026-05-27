#include <windows.h>
#include <iostream>
#include <vector>
#include <string>
#include <iomanip>
#include <thread>
#include <chrono>

// Windows HID Headers
#include <setupapi.h>
#include <hidsdi.h>
#include <hidpi.h>

#pragma comment(lib, "setupapi.lib")
#pragma comment(lib, "hid.lib")

// 設定目標裝置：Apple Magic Keyboard
const unsigned short APPLE_VENDOR_ID = 0x004C;
const unsigned short TARGET_PID = 0x029C;

// 繪製電池圖示
void DrawBatteryBar(int percentage) {
    std::wcout << L"\r[";
    int bars = percentage / 5; // 每 5% 一格，共 20 格
    for (int i = 0; i < 20; ++i) {
        if (i < bars) std::wcout << L"|";
        else std::wcout << L" ";
    }
    // 重要修正：加入 std::dec 強制使用十進位顯示，避免出現 4e (Hex)
    std::wcout << L"] " << std::dec << percentage << L"%   " << std::flush;
}

// 讀取電量的核心函式 (加入重試機制)
int GetBatteryLevel(HANDLE hDevice) {
    // 根據您的測試結果，Magic Keyboard (0x29c) 回應 ID 0x90
    // 數據格式為: [0x90] [0x00] [Level]
    unsigned char reportId = 0x90;
    unsigned char buffer[3] = { 0 };

    // 改進：嘗試讀取最多 3 次，減少 "Connection Lost" 的誤報
    // 藍牙裝置有時候會因為忙碌而拒絕單次請求
    for (int i = 0; i < 3; i++) {
        buffer[0] = reportId; // 每次重試都要重新設定 Report ID

        // 使用 HidD_GetInputReport 主動查詢
        if (HidD_GetInputReport(hDevice, buffer, sizeof(buffer))) {
            // 驗證 ID 是否正確
            if (buffer[0] == 0x90) {
                // 您的測試數據顯示電量在 index 2
                int level = buffer[2];
                return level;
            }
        }

        // 如果失敗，稍微暫停一下再重試 (50ms)
        std::this_thread::sleep_for(std::chrono::milliseconds(50));
    }

    return -1; // 3次都失敗才回傳錯誤
}

int MK_battery() {
    // 設定中文輸出環境
    std::locale::global(std::locale(""));
    std::wcout.imbue(std::locale());

    std::wcout << L"========================================" << std::endl;
    std::wcout << L"   Magic Keyboard Battery Monitor v1.1  " << std::endl;
    std::wcout << L"========================================" << std::endl;
    std::wcout << L"Target PID: 0x" << std::hex << TARGET_PID << std::endl;
    std::wcout << L"Status: Searching..." << std::endl;

    GUID hidGuid;
    HidD_GetHidGuid(&hidGuid);

    // 主迴圈：持續監控
    while (true) {
        HDEVINFO hDevInfo = SetupDiGetClassDevs(&hidGuid, NULL, NULL, DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);
        if (hDevInfo == INVALID_HANDLE_VALUE) {
            std::this_thread::sleep_for(std::chrono::seconds(5));
            continue;
        }

        SP_DEVICE_INTERFACE_DATA devInfoData;
        devInfoData.cbSize = sizeof(SP_DEVICE_INTERFACE_DATA);
        int deviceIndex = 0;
        bool deviceFound = false;

        while (SetupDiEnumDeviceInterfaces(hDevInfo, NULL, &hidGuid, deviceIndex, &devInfoData)) {
            deviceIndex++;
            DWORD requiredSize = 0;
            SetupDiGetDeviceInterfaceDetail(hDevInfo, &devInfoData, NULL, 0, &requiredSize, NULL);
            if (requiredSize == 0) continue;

            PSP_DEVICE_INTERFACE_DETAIL_DATA deviceDetail = (PSP_DEVICE_INTERFACE_DETAIL_DATA)malloc(requiredSize);
            deviceDetail->cbSize = sizeof(SP_DEVICE_INTERFACE_DETAIL_DATA);

            if (SetupDiGetDeviceInterfaceDetail(hDevInfo, &devInfoData, deviceDetail, requiredSize, NULL, NULL)) {

                // 開啟裝置 (Generic Read/Write)
                HANDLE hDevice = CreateFile(
                    deviceDetail->DevicePath,
                    GENERIC_READ | GENERIC_WRITE,
                    FILE_SHARE_READ | FILE_SHARE_WRITE,
                    NULL,
                    OPEN_EXISTING,
                    0, // 這裡不需要 Overlapped，因為 HidD_GetInputReport 是同步的
                    NULL
                );

                if (hDevice != INVALID_HANDLE_VALUE) {
                    HIDD_ATTRIBUTES attributes;
                    attributes.Size = sizeof(HIDD_ATTRIBUTES);

                    if (HidD_GetAttributes(hDevice, &attributes)) {
                        // 鎖定 Apple + 特定 PID
                        if (attributes.VendorID == APPLE_VENDOR_ID && attributes.ProductID == TARGET_PID) {

                            // 檢查是否為正確的介面 (Input Length = 3)
                            PHIDP_PREPARSED_DATA preparsedData;
                            HIDP_CAPS caps;
                            if (HidD_GetPreparsedData(hDevice, &preparsedData)) {
                                HidP_GetCaps(preparsedData, &caps);
                                HidD_FreePreparsedData(preparsedData);

                                if (caps.InputReportByteLength == 3) {
                                    deviceFound = true;
                                    int battery = GetBatteryLevel(hDevice);

                                    if (battery != -1) {
                                        DrawBatteryBar(battery);
                                    }
                                    else {
                                        // 只有真的連不上了才顯示 Lost
                                        std::wcout << L"\r[?] Connection Lost...   " << std::flush;
                                    }
                                }
                            }
                        }
                    }
                    CloseHandle(hDevice);
                }
            }
            free(deviceDetail);
            if (deviceFound) break; // 找到後就不用繼續遍歷其他介面了
        }

        SetupDiDestroyDeviceInfoList(hDevInfo);

        if (!deviceFound) {
            std::wcout << L"\r[!] Device Not Found    " << std::flush;
        }

        // 每 5 秒刷新一次
        std::this_thread::sleep_for(std::chrono::seconds(5));
    }

    return 0;
}