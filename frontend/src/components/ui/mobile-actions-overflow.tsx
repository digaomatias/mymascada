"use client";

import * as React from "react";
import Link from "next/link";
import { EllipsisVerticalIcon } from "@heroicons/react/24/outline";
import { cn } from "@/lib/utils";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";

interface MobileAction {
  id: string;
  label: string;
  icon: React.ReactNode;
  onClick?: () => void;
  href?: string;
  variant?: "default" | "danger";
  show?: boolean;
  disabled?: boolean;
}

interface MobileActionsOverflowProps {
  actions: MobileAction[];
  triggerClassName?: string;
}

/**
 * MobileActionsOverflow - A dropdown menu for secondary actions on mobile.
 *
 * Features:
 * - Uses existing shadcn/ui DropdownMenu components
 * - Supports both onClick and href (uses Next.js Link for navigation)
 * - Conditional visibility via show prop
 * - Danger variant for destructive actions
 * - Minimum 44px touch target for trigger button
 */
function MobileActionsOverflow({
  actions,
  triggerClassName,
}: MobileActionsOverflowProps) {
  // Filter out actions where show is explicitly false
  const visibleActions = actions.filter((action) => action.show !== false);

  // Don't render if no visible actions
  if (visibleActions.length === 0) {
    return null;
  }

  return (
    <DropdownMenu>
      <DropdownMenuTrigger
        className={cn(
          // Minimum 44px touch target for accessibility
          "min-w-[44px] min-h-[44px]",
          // Match secondary button styling (btn-secondary)
          "inline-flex items-center justify-center",
          "rounded-md border border-primary",
          "bg-transparent text-primary",
          "hover:bg-primary-50",
          // Focus and accessibility
          "focus:outline-none focus-visible:ring-2 focus-visible:ring-primary focus-visible:ring-offset-2",
          // Transition
          "transition-colors duration-200",
          // Size matching sm button
          "px-3 py-2 text-sm",
          triggerClassName
        )}
        aria-label="More actions"
      >
        <EllipsisVerticalIcon className="w-5 h-5" aria-hidden="true" />
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" side="bottom" className="min-w-[180px] bg-white shadow-lg border border-gray-200 rounded-lg">
        {visibleActions.map((action) => {
          const itemContent = (
            <>
              <span aria-hidden="true">{action.icon}</span>
              <span>{action.label}</span>
            </>
          );

          // If action has href, render as Link
          if (action.href) {
            return (
              <DropdownMenuItem
                key={action.id}
                asChild
                variant={action.variant === "danger" ? "destructive" : "default"}
                disabled={action.disabled}
                className="min-h-[44px] py-3 px-3"
              >
                <Link href={action.href} className="flex items-center gap-2">
                  {itemContent}
                </Link>
              </DropdownMenuItem>
            );
          }

          // Otherwise render as button with onClick
          return (
            <DropdownMenuItem
              key={action.id}
              onClick={action.disabled ? undefined : action.onClick}
              variant={action.variant === "danger" ? "destructive" : "default"}
              disabled={action.disabled}
              className="flex items-center gap-2 min-h-[44px] py-3 px-3"
            >
              {itemContent}
            </DropdownMenuItem>
          );
        })}
      </DropdownMenuContent>
    </DropdownMenu>
  );
}

export { MobileActionsOverflow };
export type { MobileAction, MobileActionsOverflowProps };
