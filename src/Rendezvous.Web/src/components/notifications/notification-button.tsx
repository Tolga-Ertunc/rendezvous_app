"use client"

import { useEffect, useState } from "react"
import Link from "next/link"
import { Bell, X } from "lucide-react"

import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog"
import {
  getNotifications,
  markAllNotificationsRead,
  markNotificationRead,
} from "@/lib/auth-api"
import type { NotificationItem } from "@/lib/auth-api"
import { cn } from "@/lib/utils"

type NotificationButtonProps = {
  className?: string
}

export function NotificationButton({ className }: NotificationButtonProps) {
  const [open, setOpen] = useState(false)
  const [notifications, setNotifications] = useState<NotificationItem[]>([])
  const [unreadCount, setUnreadCount] = useState(0)
  const [isLoading, setIsLoading] = useState(false)

  useEffect(() => {
    loadNotifications()
    const intervalId = window.setInterval(loadNotifications, 30000)

    return () => window.clearInterval(intervalId)
  }, [])

  async function loadNotifications() {
    setIsLoading(true)
    try {
      const response = await getNotifications()
      setNotifications(response.notifications)
      setUnreadCount(response.unreadCount)
    } finally {
      setIsLoading(false)
    }
  }

  async function handleMarkRead(notificationId: string) {
    await markNotificationRead(notificationId)
    await loadNotifications()
  }

  async function handleMarkAllRead() {
    await markAllNotificationsRead()
    await loadNotifications()
  }

  return (
    <>
      <Button
        type="button"
        variant="outline"
        className={cn("relative", className)}
        onClick={() => setOpen(true)}
        aria-label="Notifications"
      >
        <Bell className="size-4" aria-hidden="true" />
        {unreadCount > 0 ? (
          <span className="absolute -right-1.5 -top-1.5 flex min-w-5 items-center justify-center rounded-full bg-destructive px-1.5 text-[11px] font-semibold leading-5 text-destructive-foreground">
            {unreadCount > 99 ? "99+" : unreadCount}
          </span>
        ) : null}
      </Button>

      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Notifications</DialogTitle>
            <DialogDescription>
              Appointment and account updates for this user.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="flex items-center justify-between gap-3">
              <Badge variant="outline">{unreadCount} unread</Badge>
              <Button
                type="button"
                size="sm"
                variant="outline"
                disabled={unreadCount === 0}
                onClick={handleMarkAllRead}
              >
                Mark all as read
              </Button>
            </div>
            {isLoading && notifications.length === 0 ? (
              <p className="text-sm leading-6 text-muted-foreground">
                Loading notifications.
              </p>
            ) : notifications.length === 0 ? (
              <p className="text-sm leading-6 text-muted-foreground">
                No notifications yet.
              </p>
            ) : (
              <div className="grid max-h-[420px] gap-2 overflow-auto pr-1">
                {notifications.map((notification) => (
                  <div
                    key={notification.id}
                    className="rounded-lg border border-border bg-background p-3"
                  >
                    <div className="flex items-start justify-between gap-3">
                      <div className="min-w-0 space-y-1">
                        <div className="flex flex-wrap items-center gap-2">
                          <p className="text-sm font-medium text-foreground">
                            {notification.title}
                          </p>
                          {notification.readAtUtc ? null : <Badge>New</Badge>}
                        </div>
                        <p className="text-sm leading-5 text-muted-foreground">
                          {notification.message}
                        </p>
                        {notification.linkUrl ? (
                          <Link
                            href={notification.linkUrl}
                            className="text-sm font-medium text-primary"
                            onClick={() => setOpen(false)}
                          >
                            Open
                          </Link>
                        ) : null}
                      </div>
                      {!notification.readAtUtc ? (
                        <Button
                          type="button"
                          size="icon"
                          variant="outline"
                          aria-label="Dismiss notification"
                          onClick={() => handleMarkRead(notification.id)}
                        >
                          <X className="size-4" aria-hidden="true" />
                        </Button>
                      ) : null}
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>
        </DialogContent>
      </Dialog>
    </>
  )
}
