'use client';

import React, { createContext, useContext, useEffect, useState, ReactNode } from 'react';
import { apiClient, defaultFeatures, FeatureFlags } from '@/lib/api-client';

interface FeaturesContextType {
  features: FeatureFlags;
  isLoading: boolean;
}

const FeaturesContext = createContext<FeaturesContextType>({
  features: defaultFeatures,
  isLoading: true,
});

interface FeaturesProviderProps {
  children: ReactNode;
  // Flags fetched server-side and used to seed state, so flag-gated UI renders
  // correctly on the first paint without waiting for a client round-trip.
  initialFeatures?: FeatureFlags;
}

export function FeaturesProvider({ children, initialFeatures }: FeaturesProviderProps) {
  const [features, setFeatures] = useState<FeatureFlags>(initialFeatures ?? defaultFeatures);
  // When seeded from the server we already have flags — don't block on the client fetch.
  const [isLoading, setIsLoading] = useState(initialFeatures === undefined);

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
