'use client';

import React, { forwardRef, useState, useCallback } from 'react';
import { Input } from '@/components/ui/input';
import { cn } from '@/lib/utils';

export interface CurrencyInputProps extends Omit<React.InputHTMLAttributes<HTMLInputElement>, 'value' | 'onChange'> {
  value?: number;
  onChange?: (value: number) => void;
  currency?: string;
  locale?: string;
  minimumFractionDigits?: number;
  maximumFractionDigits?: number;
  allowNegative?: boolean;
  placeholder?: string;
  label?: string;
  error?: boolean;
  errorMessage?: string;
  className?: string;
}

export const CurrencyInput = forwardRef<HTMLInputElement, CurrencyInputProps>(
  ({
    value = 0,
    onChange,
    currency = 'NZD',
    locale = 'en-NZ',
    minimumFractionDigits = 2,
    maximumFractionDigits = 2,
    allowNegative = true,
    placeholder = 'Enter amount',
    label,
    error = false,
    errorMessage,
    className,
    onFocus,
    onBlur,
    ...props
  }, ref) => {
    const [displayValue, setDisplayValue] = useState('');
    const [isFocused, setIsFocused] = useState(false);

    // Format number to currency string
    const formatCurrency = useCallback((amount: number): string => {
      if (isNaN(amount)) return '';
      
      try {
        return new Intl.NumberFormat(locale, {
          style: 'currency',
          currency: currency,
          minimumFractionDigits,
          maximumFractionDigits,
        }).format(amount);
      } catch (error) {
        console.warn('Invalid currency format:', error);
        return `${currency} ${amount.toFixed(maximumFractionDigits)}`;
      }
    }, [locale, currency, minimumFractionDigits, maximumFractionDigits]);

    // Format number for editing (no currency symbol)
    const formatForEditing = useCallback((amount: number): string => {
      if (isNaN(amount) || amount === 0) return '';
      return amount.toFixed(maximumFractionDigits);
    }, [maximumFractionDigits]);

    // Parse string to number
    const parseValue = useCallback((str: string): number => {
      if (!str.trim()) return 0;
      
      // Remove currency symbols and formatting
      const cleanStr = str
        .replace(/[^\d.,-]/g, '') // Remove non-numeric characters except decimal separators
        .replace(/,/g, '') // Remove thousands separators
        .replace(/[^\d.-]/g, ''); // Keep only digits, decimal point, and minus sign
      
      const parsed = parseFloat(cleanStr);
      return isNaN(parsed) ? 0 : parsed;
    }, []);

    // Update display value when value prop changes
    React.useEffect(() => {
      if (!isFocused) {
        setDisplayValue(value === 0 ? '' : formatCurrency(value));
      }
    }, [value, formatCurrency, isFocused]);

    // Handle input change
    const handleChange = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
      const inputValue = e.target.value;
      setDisplayValue(inputValue);
      
      const numericValue = parseValue(inputValue);
      
      // Apply negative number restriction
      if (!allowNegative && numericValue < 0) {
        return;
      }
      
      onChange?.(numericValue);
    }, [parseValue, allowNegative, onChange]);

    // Handle focus
    const handleFocus = useCallback((e: React.FocusEvent<HTMLInputElement>) => {
      setIsFocused(true);
      
      // Convert to editing format (no currency symbol)
      const editingValue = formatForEditing(value);
      setDisplayValue(editingValue);
      
      // Select all text for easy replacement
      setTimeout(() => {
        e.target.select();
      }, 0);
      
      onFocus?.(e);
    }, [value, formatForEditing, onFocus]);

    // Handle blur
    const handleBlur = useCallback((e: React.FocusEvent<HTMLInputElement>) => {
      setIsFocused(false);
      
      // Parse the current display value and format it
      const numericValue = parseValue(displayValue);
      setDisplayValue(numericValue === 0 ? '' : formatCurrency(numericValue));
      
      // Ensure the parent component gets the final value
      onChange?.(numericValue);
      
      onBlur?.(e);
    }, [displayValue, parseValue, formatCurrency, onChange, onBlur]);

    // Handle key press for validation
    const handleKeyPress = useCallback((e: React.KeyboardEvent<HTMLInputElement>) => {
      const char = e.key;
      const currentValue = displayValue;
      
      // Allow control keys
      if (char === 'Backspace' || char === 'Delete' || char === 'Tab' || char === 'Enter' || char === 'Escape') {
        return;
      }
      
      // Allow arrow keys
      if (char === 'ArrowLeft' || char === 'ArrowRight' || char === 'ArrowUp' || char === 'ArrowDown') {
        return;
      }
      
      // Allow minus sign only at the beginning and if negative numbers are allowed
      if (char === '-' && allowNegative && !currentValue.includes('-')) {
        return;
      }
      
      // Allow decimal point only once
      if (char === '.' && !currentValue.includes('.')) {
        return;
      }
      
      // Allow digits
      if (/\d/.test(char)) {
        return;
      }
      
      // Block all other characters
      e.preventDefault();
    }, [displayValue, allowNegative]);

    return (
      <div className="space-y-2">
        {label && (
          <label className="block text-sm font-medium text-gray-700">
            {label}
          </label>
        )}
        <Input
          ref={ref}
          {...props}
          type="text"
          value={displayValue}
          onChange={handleChange}
          onFocus={handleFocus}
          onBlur={handleBlur}
          onKeyDown={handleKeyPress}
          placeholder={isFocused ? '0.00' : placeholder}
          error={error}
          className={cn(
            'text-right', // Right-align currency values
            className
          )}
          autoComplete="off"
        />
        {error && errorMessage && (
          <p className="text-sm text-red-600">{errorMessage}</p>
        )}
      </div>
    );
  }
);

CurrencyInput.displayName = 'CurrencyInput';