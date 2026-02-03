'use client';

import Link from 'next/link';
import { usePathname, useSearchParams } from 'next/navigation';
import { createTransactionDetailUrl, createTransactionEditUrl, getCurrentPageUrl, isSourcePage } from '@/lib/navigation-utils';
import { ReactNode } from 'react';

interface ContextualTransactionLinkProps {
  transactionId: number | string;
  mode?: 'view' | 'edit';
  children: ReactNode;
  className?: string;
  onClick?: (e: React.MouseEvent) => void;
}

/**
 * A smart Link component that automatically preserves navigation context
 * when linking to transaction details or edit pages.
 * 
 * Usage:
 * <ContextualTransactionLink transactionId={123}>View Transaction</ContextualTransactionLink>
 * <ContextualTransactionLink transactionId={123} mode="edit">Edit Transaction</ContextualTransactionLink>
 */
export function ContextualTransactionLink({ 
  transactionId, 
  mode = 'view',
  children, 
  className,
  onClick 
}: ContextualTransactionLinkProps) {
  const pathname = usePathname();
  const searchParams = useSearchParams();
  
  // Only preserve context if we're on a "source" page (not already on a detail page)
  const shouldPreserveContext = isSourcePage(pathname);
  const currentUrl = shouldPreserveContext ? getCurrentPageUrl(pathname, searchParams) : undefined;
  
  const href = mode === 'edit' 
    ? createTransactionEditUrl(transactionId, currentUrl)
    : createTransactionDetailUrl(transactionId, currentUrl);
  
  return (
    <Link 
      href={href} 
      className={className}
      onClick={onClick}
    >
      {children}
    </Link>
  );
}