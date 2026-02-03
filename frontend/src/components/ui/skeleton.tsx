import { HTMLAttributes, forwardRef } from 'react';
import { cn } from '@/lib/utils';

export interface SkeletonProps extends HTMLAttributes<HTMLDivElement> {
  variant?: 'default' | 'circular' | 'rounded-sm';
}

const Skeleton = forwardRef<HTMLDivElement, SkeletonProps>(
  ({ className, variant = 'default', ...props }, ref) => {
    return (
      <div
        ref={ref}
        className={cn(
          'animate-pulse bg-linear-to-r from-gray-200 via-gray-300 to-gray-200 bg-size-[200%_100%]',
          variant === 'circular' && 'rounded-full',
          variant === 'rounded-sm' && 'rounded-xl',
          variant === 'default' && 'rounded-sm',
          className
        )}
        style={{
          animation: 'shimmer 2s ease-in-out infinite',
        }}
        {...props}
      />
    );
  }
);

Skeleton.displayName = 'Skeleton';

// Stat card skeleton specifically designed for dashboard stats
const StatCardSkeleton = () => (
  <div className="card-hover bg-white/90 backdrop-blur-xs border-0 border-l-4 border-l-gray-200 shadow-lg">
    <div className="p-4 lg:p-6">
      <div className="space-y-2">
        <Skeleton className="h-4 w-24" />
        <Skeleton className="h-8 w-20" />
        <Skeleton className="h-3 w-28" />
      </div>
    </div>
  </div>
);


export { Skeleton, StatCardSkeleton };