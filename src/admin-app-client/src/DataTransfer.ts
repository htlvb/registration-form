type Slot = {
  startTime: string
  duration: string | null
  closingDate: string | null
  maxQuantityPerBooking: number | null
  remainingCapacity: number | null
  registrationsUrl: string
}

type MailTemplate = {
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
