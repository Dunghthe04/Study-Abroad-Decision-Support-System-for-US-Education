"use client";

// Tab navigation cho trang Hồ sơ Tài chính & Ngoại khóa
// Render tất cả 3 section nhưng ẩn bằng CSS để giữ state khi chuyển tab

import { useState } from "react";
import { FinancialCard } from "./FinancialCard";
import { ExtracurricularSection } from "./ExtracurricularSection";
import { AchievementsSection } from "./AchievementsSection";

type TabKey = "finance" | "activities" | "achievements";

interface TabDef {
  key: TabKey;
  icon: string;
  label: string;
}

const TABS: TabDef[] = [
  { key: "finance", icon: "💳", label: "Khả năng Tài chính" },
  { key: "activities", icon: "🎯", label: "Hoạt động Ngoại khóa" },
  { key: "achievements", icon: "🏆", label: "Giải thưởng & Nghiên cứu" },
];

export function FinancialProfileTabs() {
  const [activeTab, setActiveTab] = useState<TabKey>("finance");

  return (
    <div>
      {/* Tab Bar */}
      <div className="flex gap-1 rounded-xl border border-line bg-ground p-1 mb-6">
        {TABS.map((tab) => {
          const isActive = activeTab === tab.key;
          return (
            <button
              key={tab.key}
              onClick={() => setActiveTab(tab.key)}
              className={`flex-1 flex items-center justify-center gap-2 rounded-lg px-3 py-2.5 text-sm font-medium transition-all duration-200 ${
                isActive
                  ? "bg-surface text-ink ring-1 ring-line"
                  : "text-ink-2 hover:text-ink hover:bg-surface/60"
              }`}
            >
              <span className="text-base">{tab.icon}</span>
              <span className="hidden sm:inline">{tab.label}</span>
            </button>
          );
        })}
      </div>

      {/* Tab Panels – all rendered, hidden by CSS to preserve state */}
      <div className={activeTab === "finance" ? "" : "hidden"}>
        <FinancialCard />
      </div>
      <div className={activeTab === "activities" ? "" : "hidden"}>
        <ExtracurricularSection />
      </div>
      <div className={activeTab === "achievements" ? "" : "hidden"}>
        <AchievementsSection />
      </div>
    </div>
  );
}
