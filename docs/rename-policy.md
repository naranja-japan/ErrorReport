# NdcErrorReport → ErrorReport 命名変更方針

## 現状

- ソリューション: `Naranja/ErrorReport`
- 名前空間: `Naranja.ErrorReport`
- 出力: `ErrorReport.exe`
- GitHub: `https://github.com/naranja-japan/ErrorReport`
- 会社 DB は直参照しない。入口は `Naranja.Api`（`http://localhost:5180`）
  - `GET /api/apps/error-report/bootstrap`
  - `POST /api/db/comm/mail/reminder`
- キャプチャ保存先の DFS フォルダ名（`NDCエラー報告キャプチャ`）は現行 NDC と共有するため変更しない

置き場は `Naranja/ErrorReport`。残件の表は `Naranja/docs/transition-exceptions.md`。
