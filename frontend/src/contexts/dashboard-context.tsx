'use client';

import { createContext, useCallback, useContext, useEffect, useState } from 'react';

export type DashboardPeriod = 'month' | 'quarter';
export type DashboardTemplate = 'education' | 'advanced';

interface DashboardContextValue {
  period: DashboardPeriod;
  setPeriod: (period: DashboardPeriod) => void;
  template: DashboardTemplate;
  setTemplate: (template: DashboardTemplate) => void;
}

const DashboardContext = createContext<DashboardContextValue | null>(null);

const TEMPLATE_KEY = 'mymascada_dashboard_template';
const OLD_LAYOUT_KEY = 'mymascada_dashboard_layout';

function migrateOldLayoutKey(): DashboardTemplate {
  try {
    const oldValue = localStorage.getItem(OLD_LAYOUT_KEY);
    if (oldValue) {
      const migrated: DashboardTemplate =
        oldValue === 'classic' ? 'advanced' : 'education';
      localStorage.setItem(TEMPLATE_KEY, migrated);
      localStorage.removeItem(OLD_LAYOUT_KEY);
      return migrated;
    }
  } catch {
    // Ignore localStorage errors
  }
  return 'education';
}

function loadTemplate(): DashboardTemplate {
  try {
    const stored = localStorage.getItem(TEMPLATE_KEY);
    if (stored === 'education' || stored === 'advanced') {
      return stored;
    }
    return migrateOldLayoutKey();
  } catch {
    return 'education';
  }
}

export function DashboardProvider({ children }: { children: React.ReactNode }) {
  const [period, setPeriodState] = useState<DashboardPeriod>('month');
  const [template, setTemplateState] = useState<DashboardTemplate>('education');

  useEffect(() => {
    setTemplateState(loadTemplate());
  }, []);

  const setPeriod = useCallback((p: DashboardPeriod) => {
    setPeriodState(p);
  }, []);

  const setTemplate = useCallback((t: DashboardTemplate) => {
    setTemplateState(t);
    try {
      localStorage.setItem(TEMPLATE_KEY, t);
    } catch {
      // Ignore localStorage errors
    }
  }, []);

  return (
    <DashboardContext.Provider value={{ period, setPeriod, template, setTemplate }}>
      {children}
    </DashboardContext.Provider>
  );
}

export function useDashboard(): DashboardContextValue {
  const context = useContext(DashboardContext);
  if (!context) {
    throw new Error('useDashboard must be used within a DashboardProvider');
  }
  return context;
}
