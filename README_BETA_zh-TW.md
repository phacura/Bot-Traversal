# Bot Traversal 0.9.0 Beta18 / RC155

修復學習定義達256筆附近時，新示範在登記前遭容量清理、造成後續動作／排隊無法開始的問題。保護既有120秒候選生命期、活動任務的原始子路段與技能模板；增加有界Vault／Climb入口取得時間；替代／自救排序接回目前skillevidence。

關閉遊戲後，將PLUGIN.zip內BepInEx/plugins/BotTraversal/BotTraversal.dll覆蓋至遊戲同一路徑。保留全部Data/Learning與設定，不需要清空或重設；本包未包含它們，也尚未自動安裝。舊bigmap.json／bigmap.experience.json不再日常更新屬目前設計，仍請保留，詳見報告。

遊戲與BepInEx版本為0.9.0，詳細建置為V2-RC155-Beta18，AssemblyInformationalVersion為0.9.0-beta.18+RC155。入口對位Run、彎中允許Sprint及WT-only自救維持；穿透關閉時維持同入口排隊與前者完成後至少一秒間隔。

SOURCE.zip包含原始碼、既有與新增離線檢查、選取證據及報告。預設參考D:\Games\SIT_DIR\SPT AKI 4.0.13的Managed與BepInEx/core。離線檢查不執行Unity場景，實機效果待新版驗收。
