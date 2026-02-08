'use client';

import React, { createContext, useContext, useEffect, useState, ReactNode } from 'react';
import { apiClient, FeatureFlags } from '@/lib/api-client';

interface FeaturesContextType {
  features: FeatureFlags;
  isLoading: boolean;
}

const defaultFeatures: FeatureFlags = {
  aiCategorization: false,
  googleOAuth: false,
  bankSync: false,
  emailNotifications: false,
  accountSharing: false,
};

const FeaturesContext = createContext<FeaturesContextType>({
  features: defaultFeatures,
  isLoading: true,
});

interface FeaturesProviderProps {
  children: ReactNode;
}

export function FeaturesProvider({ children }: FeaturesProviderProps) {
  const [features, setFeatures] = useState<FeatureFlags>(defaultFeatures);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;

    async function loadFeatures() {
      try {
        const flags = await apiClient.getFeatures();
        if (!cancelled) {
          setFeatures(flags);
        }
      } catch (error) {
        console.error('Failed to load feature flags:', error);
        // Keep defaults (all disabled) on error
      } finally {
        if (!cancelled) {
          setIsLoading(false);
        }
      }
    }

    loadFeatures();

    return () => {
      cancelled = true;
    };
  }, []);

  return (
    <FeaturesContext.Provider value={{ features, isLoading }}>
      {children}
    </FeaturesContext.Provider>
  );
}

export function useFeatures(): FeaturesContextType {
  return useContext(FeaturesContext);
}
