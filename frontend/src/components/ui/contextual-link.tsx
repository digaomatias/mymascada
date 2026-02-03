'use client';

import React from 'react';
import Link from 'next/link';
import { usePathname, useSearchParams } from 'next/navigation';

interface ContextualLinkProps {
  href: string;
  children: React.ReactNode;
  className?: string;
  onClick?: () => void;
  preserveContext?: boolean;
  target?: string;
  rel?: string;
}

/**
 * Enhanced Link component that automatically preserves navigation context
 * 
 * This component wraps Next.js Link and automatically tracks when users
 * navigate from source pages (like transaction lists) to detail pages.
 * The navigation context is stored and can be used for smart back navigation.
 * 
 * Usage:
 * <ContextualLink href="/transactions/123">View Transaction</ContextualLink>
 * 
 * The component automatically detects if you're linking from a source page
 * to a detail page and preserves the context accordingly.
 */
export function ContextualLink({ 
  href, 
  children, 
  className, 
  onClick,
  preserveContext = true,
  target,
  rel,
  ...props 
}: ContextualLinkProps) {
  const pathname = usePathname();
  const searchParams = useSearchParams();

  // Get current full URL with search params
  const getCurrentUrl = (): string => {
    const params = searchParams.toString();
    return params ? `${pathname}?${params}` : pathname;
  };

  // Check if we're on a source page that should be tracked
  const isSourcePage = (): boolean => {
    const sourcePaths = [
      '/transactions',
      '/accounts',
      '/categories',
      '/reports',
      /^\/accounts\/\d+$/,
      /^\/categories\/\d+$/,
      /^\/reports\/.+$/
    ];
    
    return sourcePaths.some(path => 
      typeof path === 'string' ? pathname.startsWith(path) : path.test(pathname)
    );
  };

  // Check if the target href is a detail page
  const isTargetDetailPage = (targetHref: string): boolean => {
    const [path] = targetHref.split('?');
    return /\/(transactions|accounts|categories)\/\d+(\/(edit|reconcile))?$/.test(path);
  };

  // Generate human-readable label for current page
  const generateLabel = (url: string): string => {
    const [path, queryString] = url.split('?');
    const params = new URLSearchParams(queryString || '');
    
    if (path === '/transactions') {
      const filters = [];
      if (params.get('account')) filters.push('filtered by account');
      if (params.get('category')) filters.push('filtered by category');
      if (params.get('search')) filters.push('search results');
      if (params.get('page') && params.get('page') !== '1') filters.push(`page ${params.get('page')}`);
      
      return filters.length > 0 ? `Transactions (${filters.join(', ')})` : 'Transactions';
    }
    
    if (path === '/accounts') return 'Accounts';
    if (path.match(/^\/accounts\/\d+$/)) return 'Account Details';
    if (path === '/categories') return 'Categories';
    if (path.match(/^\/categories\/\d+$/)) return 'Category Details';
    
    if (path.startsWith('/reports/')) {
      const reportType = path.split('/')[2];
      return `${reportType?.charAt(0).toUpperCase()}${reportType?.slice(1)} Report` || 'Report';
    }
    
    return 'Previous Page';
  };

  // Save navigation context when needed
  const saveNavigationContext = (): void => {
    if (!preserveContext) return;
    
    // Only save context when navigating from source page to detail page
    if (isSourcePage() && isTargetDetailPage(href)) {
      try {
        const currentUrl = getCurrentUrl();
        const context = {
          from: currentUrl,
          label: generateLabel(currentUrl),
          timestamp: Date.now()
        };
        
        sessionStorage.setItem('navigation-context', JSON.stringify(context));
      } catch (error) {
        console.warn('Failed to save navigation context:', error);
      }
    }
  };

  const handleClick = () => {
    saveNavigationContext();
    onClick?.();
  };

  return (
    <Link 
      href={href} 
      className={className}
      onClick={handleClick}
      target={target}
      rel={rel}
      {...props}
    >
      {children}
    </Link>
  );
}

/**
 * Specialized transaction link component
 * Automatically formats the href and preserves context
 */
interface TransactionLinkProps extends Omit<ContextualLinkProps, 'href'> {
  transactionId: number | string;
}

export function TransactionLink({ transactionId, children, ...props }: TransactionLinkProps) {
  return (
    <ContextualLink href={`/transactions/${transactionId}`} {...props}>
      {children}
    </ContextualLink>
  );
}

/**
 * Specialized account link component
 * Automatically formats the href and preserves context
 */
interface AccountLinkProps extends Omit<ContextualLinkProps, 'href'> {
  accountId: number | string;
}

export function AccountLink({ accountId, children, ...props }: AccountLinkProps) {
  return (
    <ContextualLink href={`/accounts/${accountId}`} {...props}>
      {children}
    </ContextualLink>
  );
}

/**
 * Specialized category link component  
 * Automatically formats the href and preserves context
 */
interface CategoryLinkProps extends Omit<ContextualLinkProps, 'href'> {
  categoryId: number | string;
}

export function CategoryLink({ categoryId, children, ...props }: CategoryLinkProps) {
  return (
    <ContextualLink href={`/categories/${categoryId}`} {...props}>
      {children}
    </ContextualLink>
  );
}