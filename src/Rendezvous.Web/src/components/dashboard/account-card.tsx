import { UserRound } from "lucide-react"

import { Badge } from "@/components/ui/badge"
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card"
import type { CurrentUser } from "@/lib/auth-api"

export function AccountCard({ user }: { user: CurrentUser }) {
  return (
    <Card>
      <CardHeader>
        <div className="flex items-center gap-2">
          <UserRound className="size-4 text-primary" aria-hidden="true" />
          <CardTitle>Account</CardTitle>
        </div>
        <CardDescription>Profile identity and global roles.</CardDescription>
      </CardHeader>
      <CardContent className="space-y-5">
        <dl className="grid gap-4 text-sm">
          <div className="space-y-1">
            <dt className="text-muted-foreground">Email</dt>
            <dd className="break-all font-medium text-foreground">
              {user.email}
            </dd>
          </div>
          <div className="space-y-1">
            <dt className="text-muted-foreground">Public number</dt>
            <dd className="font-medium text-foreground">{user.publicNumber}</dd>
          </div>
          <div className="space-y-2">
            <dt className="text-muted-foreground">Global roles</dt>
            <dd className="flex flex-wrap gap-2">
              {user.roles.map((role) => (
                <Badge key={role}>{role}</Badge>
              ))}
            </dd>
          </div>
        </dl>
      </CardContent>
    </Card>
  )
}
