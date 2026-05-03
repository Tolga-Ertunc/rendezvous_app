"use client"

import * as React from "react"

import { cn } from "@/lib/utils"

type TabsContextValue = {
  value: string
  setValue: (value: string) => void
}

const TabsContext = React.createContext<TabsContextValue | null>(null)

function useTabs() {
  const context = React.useContext(TabsContext)

  if (!context) {
    throw new Error("Tabs components must be used within Tabs")
  }

  return context
}

function Tabs({
  defaultValue,
  value,
  onValueChange,
  className,
  ...props
}: React.ComponentProps<"div"> & {
  defaultValue?: string
  value?: string
  onValueChange?: (value: string) => void
}) {
  const [internalValue, setInternalValue] = React.useState(defaultValue ?? "")
  const selectedValue = value ?? internalValue

  function setValue(nextValue: string) {
    setInternalValue(nextValue)
    onValueChange?.(nextValue)
  }

  return (
    <TabsContext.Provider value={{ value: selectedValue, setValue }}>
      <div data-slot="tabs" className={cn("grid gap-4", className)} {...props} />
    </TabsContext.Provider>
  )
}

function TabsList({ className, ...props }: React.ComponentProps<"div">) {
  return (
    <div
      data-slot="tabs-list"
      role="tablist"
      className={cn(
        "inline-flex w-full max-w-full items-center gap-1 overflow-x-auto rounded-lg border border-border bg-card p-1 text-muted-foreground shadow-xs sm:w-fit",
        className
      )}
      {...props}
    />
  )
}

function TabsTrigger({
  value,
  className,
  ...props
}: React.ComponentProps<"button"> & { value: string }) {
  const tabs = useTabs()
  const isSelected = tabs.value === value

  return (
    <button
      data-slot="tabs-trigger"
      role="tab"
      type="button"
      aria-selected={isSelected}
      data-state={isSelected ? "active" : "inactive"}
      className={cn(
        "inline-flex h-8 shrink-0 items-center justify-center rounded-md px-3 text-sm font-medium whitespace-nowrap transition-colors outline-none focus-visible:ring-3 focus-visible:ring-ring/35 disabled:pointer-events-none disabled:opacity-50 data-[state=active]:bg-primary/10 data-[state=active]:text-primary",
        className
      )}
      onClick={() => tabs.setValue(value)}
      {...props}
    />
  )
}

function TabsContent({
  value,
  className,
  ...props
}: React.ComponentProps<"div"> & { value: string }) {
  const tabs = useTabs()

  if (tabs.value !== value) {
    return null
  }

  return (
    <div
      data-slot="tabs-content"
      role="tabpanel"
      className={cn("outline-none", className)}
      {...props}
    />
  )
}

export { Tabs, TabsContent, TabsList, TabsTrigger }
