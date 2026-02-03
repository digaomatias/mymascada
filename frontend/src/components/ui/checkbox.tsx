"use client"

import * as React from "react"
import { CheckIcon } from "@heroicons/react/24/outline"
import { cn } from "@/lib/utils"

interface CheckboxProps {
  checked?: boolean;
  onCheckedChange?: (checked: boolean) => void;
  disabled?: boolean;
  className?: string;
  id?: string;
}

const Checkbox = React.forwardRef<HTMLInputElement, CheckboxProps>(
  ({ className, checked, onCheckedChange, disabled, id, ...props }, ref) => (
    <div className="relative inline-flex items-center">
      <input
        ref={ref}
        type="checkbox"
        id={id}
        checked={checked}
        onChange={(e) => onCheckedChange?.(e.target.checked)}
        disabled={disabled}
        className="sr-only"
        {...props}
      />
      <div
        className={cn(
          "h-4 w-4 shrink-0 rounded-sm border border-gray-300 flex items-center justify-center cursor-pointer",
          "focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 focus-visible:ring-offset-2",
          "disabled:cursor-not-allowed disabled:opacity-50",
          checked && "bg-blue-600 border-blue-600 text-white",
          className
        )}
        onClick={() => !disabled && onCheckedChange?.(!checked)}
      >
        {checked && <CheckIcon className="h-3 w-3" />}
      </div>
    </div>
  )
)
Checkbox.displayName = "Checkbox"

export { Checkbox }