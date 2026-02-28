'use client';

import { useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { useAuth } from '@/contexts/auth-context';

/**
 * Replaces scattered auth-check + redirect patterns.
 * Key difference: returns shouldRender: true even during auth loading,
 * so page shells render immediately instead of showing a full-page spinner.
 */
export function useAuthGuard() {
  const { isAuthenticated, isLoading } = useAuth();
  const router = useRouter();

  useEffect(() => {
    if (!isLoading && !isAuthenticated) {
      router.push('/auth/login');
    }
  }, [isAuthenticated, isLoading, router]);

  return {
    /** true during auth loading AND when authenticated — only false during redirect */
    shouldRender: isLoading || isAuthenticated,
    /** true only after auth is confirmed */
    isAuthResolved: !isLoading && isAuthenticated,
  };
}
