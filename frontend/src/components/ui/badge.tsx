import * as React from "react"
import { cn } from "@/lib/utils"

const badgeVariants = {
  default: "inline-flex items-center rounded-full border border-transparent bg-primary-500 text-white px-2.5 py-0.5 text-xs font-semibold",
  secondary: "inline-flex items-center rounded-full border border-transparent bg-gray-100 text-gray-900 px-2.5 py-0.5 text-xs font-semibold",
  destructive: "inline-flex items-center rounded-full border border-transparent bg-red-500 text-white px-2.5 py-0.5 text-xs font-semibold",
  outline: "inline-flex items-center rounded-full border border-gray-300 text-gray-900 px-2.5 py-0.5 text-xs font-semibold",
}

export interface BadgeProps extends React.HTMLAttributes<HTMLDivElement> {
  variant?: keyof typeof badgeVariants;
}

function Badge({ className, variant = 'default', ...props }: BadgeProps) {
  return (
    <div className={cn(badgeVariants[variant], className)} {...props} />
  )
}

export { Badge, badgeVariants }