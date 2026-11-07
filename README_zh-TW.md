# Bot Traversal 1.0.1 / RC158

適用 SPT AKI 4.1.6 + PIT Fireteam 1.1.1。

修復蹲姿預檢通過後，實際防撞仍用站姿膠囊而把前進輸入裁成零；統一車頂 WT、蹲行、轉角與出口的原生查詢包絡。修正地面狀態切換時快照遺失 RequiredPose，以及已發布段落被舊快照重新接回的問題。

關閉遊戲，將 PLUGIN ZIP 的 BepInEx/plugins/BotTraversal/BotTraversal.dll 放入 4.1.6 對應目錄，保留設定與 Data/Learning。請重新示範車頂／蹲行一次，包含入口蹲下、完整通過與出口站起；不需要重設其他路段。

公開版本 1.0.1；組件／檔案版本 1.0.1.0；標記 V2-RC158-SPT416；詳細版本 1.0.1+RC158.SPT416。未自動安裝，實際車體通行仍待新版實機驗收。

SOURCE ZIP 內含原始碼與離線檢查，預設參考 D:\Games\SIT_DIR\SPT AKI 4.1.6 的 Managed、BepInEx/core 及必要依賴。完整檢查為 QA/Run-CorridorQA.ps1；成品重建為 QA/Verify-SourceArtifact.py。
