export type Slot = {
  startTime: string
  duration: string | null
  closingDate: string | null
  maxQuantityPerBooking: number | null
  remainingCapacity: number | null
  registrationsUrl: string
}

export type MailTemplate = {
  subject: string
  contentTemplate: string
}
export type Event = {
  type: 'draft' | 'released' | 'archived'
  key: string
  title: string
  infoText: string
  reservationStartTime: string
  slots: Slot[]
  registrationConfirmationMail: MailTemplate
  requestConfirmationMail: MailTemplate | null
  url: string
  canDelete: boolean
  canEditEventData: boolean
  canAddSlot: boolean
  canEditSlot: boolean
  canDeleteSlot: boolean
  canViewRegistrations: boolean
}

export type PatchEventDataSlot =
  {
    type: 'create'
    startTime: string
    duration: string | null
    closingDate: string | null
    maxQuantityPerBooking: number | null
    remainingCapacity: number | null
    canRequestIfFullyBooked: boolean
  } |
  {
    type: 'update'
    startTime: string
    newTime: string | null
    duration: string | null
    setDuration: boolean
    closingDate: string | null
    setClosingDate: boolean
    maxQuantityPerBooking: number | null
    setMaxQuantityPerBooking: boolean
    remainingCapacity: number | null
    setRemainingCapacity: boolean
    canRequestIfFullyBooked: boolean | null
  } |
  {
    type: 'delete'
    startTime: string
  }
export type PatchEventData = {
  key: string | null
  title: string | null
  infoText: string | null
  reservationStartTime: string | null
  slots: PatchEventDataSlot[]
  registrationConfirmationMail: MailTemplate | null
  requestConfirmationMail: MailTemplate | null
  setRequestConfirmationMail: boolean
}
