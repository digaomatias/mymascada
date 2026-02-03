'use client';

import { useRouter, useSearchParams } from 'next/navigation';
import { Button } from '@/components/ui/button';
import { ArrowLeftIcon } from '@heroicons/react/24/outline';
import { getReturnUrl, getReturnUrlLabel } from '@/lib/navigation-utils';

interface SmartBackButtonProps {
  fallbackUrl?: string;
  className?: string;
  variant?: 'primary' | 'secondary' | 'danger' | 'ghost';
  size?: 'sm' | 'md' | 'lg';
  showLabel?: boolean;
}

/**
 * A smart back button that preserves navigation context.
 * Uses return URL from query parameters, with intelligent fallbacks.
 * 
 * Fallback order:
 * 1. returnUrl query parameter (preserved context)
 * 2. Browser history (if available)
 * 3. Provided fallbackUrl
 * 4. /transactions (default)
 */
export function SmartBackButton({ 
  fallbackUrl = '/transactions',
  className,
  variant = 'secondary',
  size = 'md',
  showLabel = true
}: SmartBackButtonProps) {
  const router = useRouter();
  const searchParams = useSearchParams();
  
  const returnUrl = getReturnUrl(searchParams);
  const returnLabel = getReturnUrlLabel(returnUrl);
  
  const handleBack = () => {
    if (searchParams.get('returnUrl')) {
      // Use preserved context
      router.push(returnUrl);
    } else if (window.history.length > 1) {
      // Try browser back
      router.back();
    } else {
      // Fallback to provided URL
      router.push(fallbackUrl);
    }
  };
  
  return (
    <Button
      variant={variant}
      size={size}
      onClick={handleBack}
      className={`flex items-center gap-2 ${className || ''}`}
    >
      <ArrowLeftIcon className="w-4 h-4" />
      {showLabel && (
        <span className="hidden sm:inline">
          Back to {returnLabel}
        </span>
      )}
      {showLabel && (
        <span className="sm:hidden">
          Back
        </span>
      )}
    </Button>
  );
}

/**
 * Pre-configured back button for transaction pages
 */
export function TransactionBackButton(props: Partial<SmartBackButtonProps>) {
  return (
    <SmartBackButton
      fallbackUrl="/transactions"
      {...props}
    />
  );
}