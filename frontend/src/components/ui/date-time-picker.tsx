'use client';

import React, { useState, useRef, useEffect } from 'react';
import { createPortal } from 'react-dom';
import { DayPicker } from 'react-day-picker';
import TimePicker from 'react-time-picker';
import { format, parseISO, isValid } from 'date-fns';
import { CalendarIcon, ClockIcon, XMarkIcon } from '@heroicons/react/24/outline';
import { cn } from '@/lib/utils';
import { useTranslations } from 'next-intl';
import 'react-day-picker/dist/style.css';
import 'react-time-picker/dist/TimePicker.css';
import 'react-clock/dist/Clock.css';

interface DateTimePickerProps {
  value?: string; // ISO string format
  onChange: (value: string) => void;
  placeholder?: string;
  disabled?: boolean;
  className?: string;
  showTime?: boolean;
  label?: string;
  error?: string;
}

export function DateTimePicker({
  value,
  onChange,
  placeholder,
  disabled = false,
  className,
  showTime = true,
  label,
  error
}: DateTimePickerProps) {
  const tCommon = useTranslations('common');
  const tTime = useTranslations('time');
  const [isOpen, setIsOpen] = useState(false);
  const [selectedDate, setSelectedDate] = useState<Date | undefined>(
    value ? parseISO(value) : undefined
  );
  const [selectedTime, setSelectedTime] = useState<string>('10:00');
  const [dropdownPosition, setDropdownPosition] = useState({ top: 0, left: 0 });
  const [isMounted, setIsMounted] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);
  const buttonRef = useRef<HTMLButtonElement>(null);
  const dropdownRef = useRef<HTMLDivElement>(null);
  const resolvedPlaceholder = placeholder || tCommon('selectDateTime');

  // Track mounted state for portal rendering
  useEffect(() => {
    setIsMounted(true);
  }, []);

  // Initialize time from existing value
  useEffect(() => {
    if (value && isValid(parseISO(value))) {
      const date = parseISO(value);
      setSelectedDate(date);
      setSelectedTime(format(date, 'HH:mm'));
    }
  }, [value]);

  // Handle escape key and click outside to close
  useEffect(() => {
    function handleEscapeKey(event: KeyboardEvent) {
      if (event.key === 'Escape') {
        setIsOpen(false);
      }
    }

    function handleClickOutside(event: MouseEvent) {
      if (
        dropdownRef.current &&
        !dropdownRef.current.contains(event.target as Node) &&
        buttonRef.current &&
        !buttonRef.current.contains(event.target as Node)
      ) {
        setIsOpen(false);
      }
    }

    if (isOpen) {
      document.addEventListener('keydown', handleEscapeKey);
      document.addEventListener('mousedown', handleClickOutside);
    }

    return () => {
      document.removeEventListener('keydown', handleEscapeKey);
      document.removeEventListener('mousedown', handleClickOutside);
    };
  }, [isOpen]);

  // Calculate dropdown position when opening
  useEffect(() => {
    if (isOpen && buttonRef.current) {
      const rect = buttonRef.current.getBoundingClientRect();
      const scrollY = window.scrollY;
      const scrollX = window.scrollX;

      // Position below the button
      let top = rect.bottom + scrollY + 8;
      let left = rect.left + scrollX;

      // Check if dropdown would go off-screen to the right
      const dropdownWidth = 320; // w-80 = 20rem = 320px
      if (left + dropdownWidth > window.innerWidth) {
        left = window.innerWidth - dropdownWidth - 16;
      }

      // Check if dropdown would go off-screen at the bottom
      const dropdownHeight = 400; // approximate max height
      if (top + dropdownHeight > window.innerHeight + scrollY) {
        // Position above the button instead
        top = rect.top + scrollY - dropdownHeight - 8;
      }

      setDropdownPosition({ top, left });
    }
  }, [isOpen]);

  const handleDateSelect = (date: Date | undefined) => {
    if (date) {
      setSelectedDate(date);
      
      if (showTime) {
        // Combine date with current time
        const [hours, minutes] = selectedTime.split(':').map(Number);
        const combined = new Date(date);
        combined.setHours(hours, minutes, 0, 0);
        onChange(combined.toISOString());
      } else {
        // Just the date at midnight in local timezone
        const combined = new Date(date);
        combined.setHours(0, 0, 0, 0);
        onChange(combined.toISOString());
        setIsOpen(false);
      }
    }
  };

  const handleTimeChange = (time: string | null) => {
    if (time && selectedDate) {
      setSelectedTime(time);
      const [hours, minutes] = time.split(':').map(Number);
      const combined = new Date(selectedDate);
      combined.setHours(hours, minutes, 0, 0);
      onChange(combined.toISOString());
    }
  };

  const handleClear = () => {
    setSelectedDate(undefined);
    setSelectedTime('10:00');
    onChange('');
  };

  const displayValue = selectedDate 
    ? showTime 
      ? `${format(selectedDate, 'MMM dd, yyyy')} ${tCommon('at')} ${selectedTime}`
      : format(selectedDate, 'MMM dd, yyyy')
    : '';

  return (
    <div className={cn("relative date-time-picker-container", className)} ref={containerRef}>
      {label && (
        <label className="block text-sm font-medium text-gray-700 mb-2">
          {label}
        </label>
      )}
      
      {/* Input Field */}
      <div className="relative">
        <button
          ref={buttonRef}
          type="button"
          onClick={() => !disabled && setIsOpen(!isOpen)}
          disabled={disabled}
          className={cn(
            "w-full px-4 py-3 text-left bg-white border rounded-xl shadow-sm transition-all duration-200",
            "hover:border-primary-300 focus:outline-none focus:ring-2 focus:ring-primary-200 focus:border-primary-400",
            "flex items-center justify-between",
            disabled && "bg-gray-50 cursor-not-allowed",
            error && "border-red-300 focus:ring-red-200 focus:border-red-400"
          )}
        >
          <span className={cn(
            "flex items-center gap-2",
            !displayValue && "text-gray-500"
          )}>
            <CalendarIcon className="w-5 h-5 text-gray-400" />
            {displayValue || resolvedPlaceholder}
          </span>
        </button>
        
        {displayValue && !disabled && (
          <button
            type="button"
            onClick={(e) => {
              e.stopPropagation();
              handleClear();
            }}
            className="absolute right-3 top-1/2 transform -translate-y-1/2 p-1 hover:bg-gray-100 rounded-full transition-colors"
          >
            <XMarkIcon className="w-4 h-4 text-gray-400" />
          </button>
        )}

        {error && (
          <p className="mt-1 text-sm text-red-600">{error}</p>
        )}
      </div>

      {/* Dropdown - rendered via portal to avoid overflow issues */}
      {isOpen && isMounted && createPortal(
        <div
          ref={dropdownRef}
          className="fixed bg-white rounded-xl shadow-2xl border border-gray-200 overflow-hidden animate-fade-in-up w-80"
          style={{
            zIndex: 99999,
            top: dropdownPosition.top,
            left: dropdownPosition.left,
          }}
          onClick={(e) => e.stopPropagation()}>
          {/* Compact Header */}
          <div className="bg-gradient-to-r from-primary-500 to-primary-600 px-4 py-3">
            <h3 className="text-white font-medium text-base flex items-center gap-2">
              <CalendarIcon className="w-4 h-4" />
              Select Date
            </h3>
          </div>

          {/* Calendar */}
          <div className="px-4 py-3">
            <DayPicker
              mode="single"
              selected={selectedDate}
              onSelect={handleDateSelect}
              showOutsideDays
              className="w-full"
              classNames={{
                months: "flex flex-col w-full",
                month: "w-full",
                caption: "flex justify-center pb-2 relative items-center w-full",
                caption_label: "text-base font-semibold text-gray-900",
                nav: "space-x-1 flex items-center",
                nav_button: cn(
                  "inline-flex items-center justify-center rounded-md w-7 h-7",
                  "hover:bg-primary-100 focus:outline-none focus:ring-2 focus:ring-primary-200",
                  "transition-colors"
                ),
                nav_button_previous: "absolute left-1",
                nav_button_next: "absolute right-1",
                table: "w-full border-collapse mx-auto",
                head_row: "flex w-full justify-between",
                head_cell: "text-gray-600 rounded flex-1 font-medium text-xs text-center pb-1",
                row: "flex w-full justify-between mt-0.5",
                cell: "flex-1 p-0 text-center text-sm focus-within:relative focus-within:z-20",
                day: cn(
                  "h-8 w-8 mx-auto p-0 font-normal rounded-md hover:bg-primary-100 text-sm",
                  "focus:outline-none focus:ring-1 focus:ring-primary-200",
                  "transition-all duration-150"
                ),
                day_today: "bg-primary-50 text-primary-700 font-semibold",
                day_selected: cn(
                  "bg-gradient-to-r from-primary-500 to-primary-600 text-white",
                  "hover:from-primary-600 hover:to-primary-700",
                  "focus:from-primary-600 focus:to-primary-700"
                ),
                day_outside: "text-gray-300 hover:bg-gray-50",
                day_disabled: "text-gray-300 opacity-50 cursor-not-allowed hover:bg-transparent",
                day_hidden: "invisible",
              }}
            />

            {/* Time Picker */}
            {showTime && selectedDate && (
              <div className="mt-4 pt-4 border-t border-gray-100">
                <div className="flex items-center gap-2 mb-3">
                  <ClockIcon className="w-4 h-4 text-gray-400" />
                  <label className="text-sm font-medium text-gray-700">{tCommon('time')}</label>
                </div>
                
                <div className="flex justify-center">
                  <div className="relative">
                    <TimePicker
                      value={selectedTime}
                      onChange={handleTimeChange}
                      className="time-picker-custom"
                      disableClock={false}
                      format="HH:mm"
                    />
                  </div>
                </div>
              </div>
            )}

            {/* Compact Footer */}
            <div className="mt-2 pt-2 border-t border-gray-100 flex items-center justify-between">
              <button
                type="button"
                onClick={() => {
                  const today = new Date();
                  setSelectedDate(today);
                  if (showTime) {
                    setSelectedTime(format(today, 'HH:mm'));
                    onChange(today.toISOString());
                  } else {
                    const todayMidnight = new Date(today);
                    todayMidnight.setHours(0, 0, 0, 0);
                    onChange(todayMidnight.toISOString());
                    setIsOpen(false);
                  }
                }}
                className="px-3 py-1.5 text-sm text-primary-600 hover:text-primary-700 font-medium transition-colors"
              >
                {tTime('today')}
              </button>
              
              <button
                type="button"
                onClick={() => setIsOpen(false)}
                className="px-4 py-1.5 bg-gradient-to-r from-primary-500 to-primary-600 text-white rounded-md hover:from-primary-600 hover:to-primary-700 transition-all duration-200 font-medium text-sm"
              >
                {tCommon('done')}
              </button>
            </div>
          </div>
        </div>,
        document.body
      )}

      {/* Custom Styles */}
      <style jsx global>{`
        /* Ensure parent containers don't clip the calendar */
        .date-time-picker-container {
          overflow: visible !important;
        }

        .time-picker-custom .react-time-picker__wrapper {
          border: 2px solid #e5e7eb;
          border-radius: 12px;
          padding: 8px 12px;
          background: white;
          transition: all 0.2s;
        }

        .time-picker-custom .react-time-picker__wrapper:hover {
          border-color: rgb(var(--primary-300));
        }

        .time-picker-custom .react-time-picker__wrapper:focus-within {
          border-color: rgb(var(--primary-400));
          box-shadow: 0 0 0 3px rgba(var(--primary-200), 0.3);
        }

        .time-picker-custom .react-time-picker__inputGroup {
          display: flex;
          align-items: center;
          gap: 4px;
        }

        .time-picker-custom .react-time-picker__inputGroup__input {
          border: none;
          outline: none;
          font-size: 16px;
          font-weight: 500;
          text-align: center;
          background: transparent;
          color: #374151;
        }

        .time-picker-custom .react-time-picker__inputGroup__divider {
          color: #6b7280;
          font-weight: 600;
        }

        .time-picker-custom .react-time-picker__clock-button {
          border: none;
          background: none;
          padding: 4px;
          border-radius: 6px;
          transition: all 0.2s;
        }

        .time-picker-custom .react-time-picker__clock-button:hover {
          background: rgb(var(--primary-100));
        }

        .time-picker-clock-custom {
          border: 2px solid #e5e7eb;
          border-radius: 16px;
          background: white;
          box-shadow: 0 20px 25px -5px rgba(0, 0, 0, 0.1);
        }
      `}</style>
    </div>
  );
}
