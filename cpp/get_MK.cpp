#include <windows.h>
#include <iostream>
#include <vector>
#include <string>

// 這些是 Windows 硬體介面所需的標頭檔
// 必須包含 setupapi.h 和 hidsdi.h
#include <setupapi.h>
#include <hidsdi.h>

// 自動連結所需的 Library，省去你在專案屬性中設定的時間
#pragma comment(lib, "setupapi.lib")
#pragma comment(lib, "hid.lib")

// Apple 的廠商 ID (Vendor ID) 固定為 0x004C
const unsigned short APPLE_VENDOR_ID = 0x004C;

int get_MK() {
    // 設定 locale 
    std::locale::global(std::locale(""));
    std::wcout.imbue(std::locale());

    std::wcout << L"--- Searching for Apple HID Devices ---" << std::endl;

    // 1. 取得 HID 類別的 GUID (全域唯一識別碼)
    GUID hidGuid;
    HidD_GetHidGuid(&hidGuid);

    // 2. 取得系統中所有 HID 裝置的資訊集 (Device Information Set)
    HDEVINFO hDevInfo = SetupDiGetClassDevs(
        &hidGuid,
        NULL,
        NULL,
        DIGCF_PRESENT | DIGCF_DEVICEINTERFACE // 只找目前已連接的裝置
    );

    if (hDevInfo == INVALID_HANDLE_VALUE) {
        std::wcerr << L"Error: Unable to get device info set." << std::endl;
        return 1;
    }

    SP_DEVICE_INTERFACE_DATA devInfoData;
    devInfoData.cbSize = sizeof(SP_DEVICE_INTERFACE_DATA);

    int deviceIndex = 0;
    bool foundKeyboard = false;

    // 3. 迴圈遍歷每一個 HID 裝置
    while (SetupDiEnumDeviceInterfaces(hDevInfo, NULL, &hidGuid, deviceIndex, &devInfoData)) {
        deviceIndex++;

        // 取得裝置路徑所需的緩衝區大小
        DWORD requiredSize = 0;
        SetupDiGetDeviceInterfaceDetail(hDevInfo, &devInfoData, NULL, 0, &requiredSize, NULL);

        if (requiredSize == 0) continue; // 無法取得詳細資訊

        // 分配記憶體給裝置詳細資訊結構
        PSP_DEVICE_INTERFACE_DETAIL_DATA deviceDetail = (PSP_DEVICE_INTERFACE_DETAIL_DATA)malloc(requiredSize);
        if (!deviceDetail) continue;

        deviceDetail->cbSize = sizeof(SP_DEVICE_INTERFACE_DETAIL_DATA);

        // 取得裝置路徑 (Device Path)
        if (SetupDiGetDeviceInterfaceDetail(hDevInfo, &devInfoData, deviceDetail, requiredSize, NULL, NULL)) {

            // 4. 開啟裝置以讀取屬性
            // 我們使用 CreateFile，注意這裡只需要讀取屬性，所以權限設為 0 或 GENERIC_READ
            HANDLE hDevice = CreateFile(
                deviceDetail->DevicePath,
                GENERIC_READ | GENERIC_WRITE, // 需要讀寫權限來查詢資訊
                FILE_SHARE_READ | FILE_SHARE_WRITE,
                NULL,
                OPEN_EXISTING,
                0,
                NULL
            );

            if (hDevice != INVALID_HANDLE_VALUE) {
                HIDD_ATTRIBUTES attributes;
                attributes.Size = sizeof(HIDD_ATTRIBUTES);

                // 5. 取得 HID 屬性 (VID, PID, Version)
                if (HidD_GetAttributes(hDevice, &attributes)) {

                    // 過濾：只顯示 Apple 的裝置 (VID = 0x004C)
                    if (attributes.VendorID == APPLE_VENDOR_ID) {

                        // 取得產品名稱字串
                        wchar_t productString[128];
                        if (!HidD_GetProductString(hDevice, productString, sizeof(productString))) {
                            wcscpy_s(productString, L"Unknown Apple Device");
                        }

                        std::wcout << L"--------------------------------" << std::endl;
                        std::wcout << L"Found Device #" << deviceIndex << std::endl;
                        std::wcout << L"Product Name: " << productString << std::endl;
                        std::wcout << L"Vendor ID: 0x" << std::hex << attributes.VendorID << std::endl;
                        std::wcout << L"Product ID: 0x" << std::hex << attributes.ProductID << std::endl;
                        std::wcout << L"Device Path: " << deviceDetail->DevicePath << std::endl;

                        // 標記已找到鍵盤
                        foundKeyboard = true;
                    }
                }
                CloseHandle(hDevice);
            }
        }

        free(deviceDetail);
    }

    SetupDiDestroyDeviceInfoList(hDevInfo);

    if (!foundKeyboard) {
        std::wcout << std::endl << L"No Apple device found. Please check Bluetooth/USB connection." << std::endl;
    }
    else {
        std::wcout << std::endl << L"Search complete. Please note your Product ID." << std::endl;
    }

    std::wcout << L"Press Enter to exit..." << std::endl;
    std::cin.get();

    return 0;
}