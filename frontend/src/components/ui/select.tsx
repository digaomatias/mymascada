'use client';

import * as React from 'react';
import { ChevronDownIcon, CheckIcon } from '@heroicons/react/24/outline';
import { cn } from '@/lib/utils';

const Select = React.forwardRef<
  React.ElementRef<'select'>,
  React.ComponentPropsWithoutRef<'select'> & {
    placeholder?: string;
    error?: boolean;
  }
>(({ className, error, children, placeholder, ...props }, ref) => (
  <select
    className={cn(
      'input appearance-none bg-white',
      'bg-[url("data:image/svg+xml,%3csvg xmlns=\'http://www.w3.org/2000/svg\' fill=\'none\' viewBox=\'0 0 20 20\'%3e%3cpath stroke=\'%236b7280\' stroke-linecap=\'round\' stroke-linejoin=\'round\' stroke-width=\'1.5\' d=\'m6 8 4 4 4-4\'/%3e%3c/svg%3e")]',
      'bg-no-repeat bg-right-2 bg-center pr-8',
      error && 'border-danger focus:border-danger focus:ring-danger/20',
      className
    )}
    ref={ref}
    {...props}
  >
    {placeholder && (
      <option value="" disabled>
        {placeholder}
      </option>
    )}
    {children}
  </select>
));
Select.displayName = 'Select';

const SelectGroup = React.forwardRef<
  React.ElementRef<'div'>,
  React.ComponentPropsWithoutRef<'div'>
>(({ className, ...props }, ref) => (
  <div ref={ref} className={cn('relative', className)} {...props} />
));
SelectGroup.displayName = 'SelectGroup';

const SelectValue = React.forwardRef<
  React.ElementRef<'span'>,
  React.ComponentPropsWithoutRef<'span'> & {
    placeholder?: string;
  }
>(({ className, children, placeholder, ...props }, ref) => (
  <span
    ref={ref}
    className={cn('block truncate', className)}
    {...props}
  >
    {children || placeholder}
  </span>
));
SelectValue.displayName = 'SelectValue';

const SelectTrigger = React.forwardRef<
  React.ElementRef<'button'>,
  React.ComponentPropsWithoutRef<'button'> & {
    error?: boolean;
  }
>(({ className, error, children, ...props }, ref) => (
  <button
    ref={ref}
    type="button"
    className={cn(
      'input flex w-full items-center justify-between bg-white',
      'focus:outline-none focus:ring-2 focus:ring-primary focus:border-primary',
      error && 'border-danger focus:border-danger focus:ring-danger/20',
      className
    )}
    {...props}
  >
    {children}
    <ChevronDownIcon className="h-4 w-4 opacity-50" />
  </button>
));
SelectTrigger.displayName = 'SelectTrigger';

const SelectContent = React.forwardRef<
  React.ElementRef<'div'>,
  React.ComponentPropsWithoutRef<'div'> & {
    position?: 'item-aligned' | 'popper';
  }
>(({ className, children, position = 'item-aligned', ...props }, ref) => (
  <div
    ref={ref}
    className={cn(
      'relative z-50 max-h-96 min-w-[8rem] overflow-hidden rounded-md border bg-white text-gray-950 shadow-md',
      'animate-in fade-in-0 zoom-in-95',
      position === 'popper' &&
        'data-[side=bottom]:slide-in-from-top-2 data-[side=left]:slide-in-from-right-2 data-[side=right]:slide-in-from-left-2 data-[side=top]:slide-in-from-bottom-2',
      className
    )}
    {...props}
  >
    <div className="max-h-96 overflow-auto p-1">
      {children}
    </div>
  </div>
));
SelectContent.displayName = 'SelectContent';

const SelectLabel = React.forwardRef<
  React.ElementRef<'div'>,
  React.ComponentPropsWithoutRef<'div'>
>(({ className, ...props }, ref) => (
  <div
    ref={ref}
    className={cn('py-1.5 pl-8 pr-2 text-sm font-semibold', className)}
    {...props}
  />
));
SelectLabel.displayName = 'SelectLabel';

const SelectItem = React.forwardRef<
  React.ElementRef<'div'>,
  React.ComponentPropsWithoutRef<'div'> & {
    value: string;
  }
>(({ className, children, value, ...props }, ref) => (
  <div
    ref={ref}
    className={cn(
      'relative flex w-full cursor-default select-none items-center rounded-sm py-1.5 pl-8 pr-2 text-sm outline-none',
      'focus:bg-gray-100 focus:text-gray-900 hover:bg-gray-100',
      'data-[disabled]:pointer-events-none data-[disabled]:opacity-50',
      className
    )}
    data-value={value}
    {...props}
  >
    <span className="absolute left-2 flex h-3.5 w-3.5 items-center justify-center">
      <CheckIcon className="h-4 w-4 opacity-0 group-data-[selected]:opacity-100" />
    </span>
    {children}
  </div>
));
SelectItem.displayName = 'SelectItem';

const SelectSeparator = React.forwardRef<
  React.ElementRef<'div'>,
  React.ComponentPropsWithoutRef<'div'>
>(({ className, ...props }, ref) => (
  <div
    ref={ref}
    className={cn('-mx-1 my-1 h-px bg-gray-100', className)}
    {...props}
  />
));
SelectSeparator.displayName = 'SelectSeparator';

export {
  Select,
  SelectGroup,
  SelectValue,
  SelectTrigger,
  SelectContent,
  SelectLabel,
  SelectItem,
  SelectSeparator,
};