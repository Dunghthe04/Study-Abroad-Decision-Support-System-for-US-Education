"use client"

import * as React from "react"
import { AlertDialog as AlertDialogPrimitive } from "@base-ui/react/alert-dialog"
import type { VariantProps } from "class-variance-authority"
import { cn } from "cn"

import {
  DialogAction,
  dialogBodyClassName,
  dialogFooterVariants,
  dialogIconVariants,
  dialogOverlayClassName,
  dialogPopupBaseClassName,
  dialogSizeClasses,
  type DialogActionProps,
  type DialogSize,
  type DialogVariant,
} from "@/components/ui/dialog"

const AlertDialogVariantContext = React.createContext<DialogVariant>("action")

function AlertDialog({ ...props }: AlertDialogPrimitive.Root.Props) {
  return <AlertDialogPrimitive.Root data-slot="alert-dialog" {...props} />
}

function AlertDialogTrigger({
  className,
  ...props
}: AlertDialogPrimitive.Trigger.Props) {
  return (
    <AlertDialogPrimitive.Trigger
      data-slot="alert-dialog-trigger"
      className={cn("cursor-pointer disabled:cursor-default", className)}
      {...props}
    />
  )
}

function AlertDialogPortal({ ...props }: AlertDialogPrimitive.Portal.Props) {
  return (
    <AlertDialogPrimitive.Portal data-slot="alert-dialog-portal" {...props} />
  )
}

function AlertDialogOverlay({
  className,
  ...props
}: AlertDialogPrimitive.Backdrop.Props) {
  return (
    <AlertDialogPrimitive.Backdrop
      data-slot="alert-dialog-overlay"
      className={cn(dialogOverlayClassName, className)}
      {...props}
    />
  )
}

function AlertDialogContent({
  className,
  children,
  variant = "action",
  size,
  ...props
}: AlertDialogPrimitive.Popup.Props & {
  variant?: DialogVariant
  size?: DialogSize | "default"
}) {
  const resolvedSize: DialogSize =
    !size || size === "default"
      ? variant === "action"
        ? "sm"
        : "md"
      : size

  return (
    <AlertDialogPortal>
      <AlertDialogOverlay />
      <AlertDialogPrimitive.Popup
        data-slot="alert-dialog-content"
        data-variant={variant}
        data-size={resolvedSize}
        className={cn(
          dialogPopupBaseClassName,
          dialogSizeClasses[resolvedSize],
          className
        )}
        {...props}
      >
        <AlertDialogVariantContext.Provider value={variant}>
          {children}
        </AlertDialogVariantContext.Provider>
      </AlertDialogPrimitive.Popup>
    </AlertDialogPortal>
  )
}

function AlertDialogIcon({
  className,
  tone = "brand",
  ...props
}: React.ComponentProps<"div"> & VariantProps<typeof dialogIconVariants>) {
  const variant = React.useContext(AlertDialogVariantContext)
  return (
    <div
      data-slot="alert-dialog-icon"
      aria-hidden="true"
      className={cn(
        dialogIconVariants({ tone }),
        variant === "action" ? "mx-auto" : "self-start",
        className
      )}
      {...props}
    />
  )
}

function AlertDialogHeader({
  className,
  ...props
}: React.ComponentProps<"div">) {
  const variant = React.useContext(AlertDialogVariantContext)
  return (
    <div
      data-slot="alert-dialog-header"
      className={cn(
        "flex shrink-0 flex-col gap-1.5",
        variant === "action"
          ? "items-center text-center"
          : "items-start text-left",
        className
      )}
      {...props}
    />
  )
}

function AlertDialogBody({
  className,
  ...props
}: React.ComponentProps<"div">) {
  return (
    <div
      data-slot="alert-dialog-body"
      className={cn(dialogBodyClassName, className)}
      {...props}
    />
  )
}

function AlertDialogFooter({
  className,
  layout = "pair",
  ...props
}: React.ComponentProps<"div"> & VariantProps<typeof dialogFooterVariants>) {
  return (
    <div
      data-slot="alert-dialog-footer"
      data-layout={layout}
      className={cn(dialogFooterVariants({ layout }), className)}
      {...props}
    />
  )
}

function AlertDialogMedia({
  className,
  tone = "brand",
  ...props
}: React.ComponentProps<"div"> & VariantProps<typeof dialogIconVariants>) {
  return <AlertDialogIcon tone={tone} className={className} {...props} />
}

function AlertDialogTitle({
  className,
  ...props
}: React.ComponentProps<typeof AlertDialogPrimitive.Title>) {
  return (
    <AlertDialogPrimitive.Title
      data-slot="alert-dialog-title"
      className={cn(
        "font-heading text-[1.0625rem] font-semibold leading-snug tracking-[-0.01em] text-ink sm:text-lg",
        className
      )}
      {...props}
    />
  )
}

function AlertDialogDescription({
  className,
  ...props
}: React.ComponentProps<typeof AlertDialogPrimitive.Description>) {
  const variant = React.useContext(AlertDialogVariantContext)
  return (
    <AlertDialogPrimitive.Description
      data-slot="alert-dialog-description"
      className={cn(
        "text-sm leading-relaxed text-ink-2 *:[a]:underline *:[a]:underline-offset-3 *:[a]:hover:text-ink",
        variant === "action" ? "text-balance" : "text-pretty",
        className
      )}
      {...props}
    />
  )
}

function AlertDialogAction({
  className,
  tone = "primary",
  ...props
}: DialogActionProps) {
  return (
    <DialogAction
      data-slot="alert-dialog-action"
      tone={tone}
      className={className}
      {...props}
    />
  )
}

function AlertDialogCancel({
  className,
  tone = "tint",
  loading = false,
  ...props
}: Omit<AlertDialogPrimitive.Close.Props, "render"> & {
  tone?: "tint" | "ghost"
  loading?: boolean
}) {
  return (
    <AlertDialogPrimitive.Close
      data-slot="alert-dialog-cancel"
      render={
        <DialogAction
          tone={tone}
          loading={loading}
          type="button"
          className={className}
        />
      }
      {...props}
    />
  )
}

export {
  AlertDialog,
  AlertDialogAction,
  AlertDialogBody,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogIcon,
  AlertDialogMedia,
  AlertDialogOverlay,
  AlertDialogPortal,
  AlertDialogTitle,
  AlertDialogTrigger,
}
