# ZipUnziper

以 **Avalonia UI** 打造的跨平台（主打 macOS）ZIP 工具，提供工具列、路徑列、多欄位檔案清單與狀態列。

## 功能

- **預覽 ZIP**：開啟後顯示名稱、類型、大小、壓縮後大小、壓縮率、修改日期、CRC32、路徑
- **資料夾導覽**：雙擊資料夾進入；上一層 / 根目錄；搜尋檔名
- **解壓縮**：全部解壓或僅解壓選取項目（含資料夾子項目）
- **壓縮**：選擇檔案或資料夾建立 ZIP
- **拖放**：拖入 `.zip` 預覽；拖入一般檔案/資料夾開始壓縮
- **快捷鍵**：`⌘O` 開啟、`⌘E` 解壓、`⌘N` 壓縮、`⌘W` 關閉、`F5` 重新整理、`⌫` 上一層

## 需求

- .NET 10 SDK
- macOS 10.15+（亦支援 Windows / Linux）

## 建置與執行

```bash
dotnet restore
dotnet build
dotnet run
```

Release：

```bash
dotnet publish -c Release -r osx-arm64 --self-contained false
```

（Intel Mac 使用 `-r osx-x64`）

## 專案結構

```
Models/          ZIP 項目與進度模型
Services/        ZipService、檔案對話框
ViewModels/      MainWindowViewModel（MVVM）
Views/           應用程式主視窗
```

## 技術

- Avalonia 11 + Fluent Theme
- CommunityToolkit.Mvvm
- `System.IO.Compression`（ZIP）
