export type Slot = {
  startTime: string
  duration: string | null
  closingDate: string | null
  maxQuantityPerBooking: number | null
  remainingCapacity: number | null
  canRequestIfFullyBooked: boolean
  registrations: EventRegistration[]
}

export type RegistrationField = {
  key: string
  label: string
  type: 'text' | 'mail' | 'phone'
  validator: 'none' | 'notEmpty'
}

export type EventRegistration = {
  quantity: number
  data: Record<string, string> // values of registration fields by key
  timestamp: string
  isRequest: boolean
  deregistrationTime: string | null
  url: string
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
  registrationFields: RegistrationField[]
  url: string
  publicRegistrationUrl: string
  canDelete: boolean
  canEditEventData: boolean
  canAddSlot: boolean
  canEditSlot: boolean
  canDeleteSlot: boolean
  canEditRegistrationFields: boolean
  canViewRegistrations: boolean
}

export type EventList = {
  events: Event[]
  createEventUrl: string | null
}

export type CreateEventDataSlot = {
  startTime: string
  duration: string | null
  closingDate: string | null
  maxQuantityPerBooking: number | null
  remainingCapacity: number | null
  canRequestIfFullyBooked: boolean
}
export type CreateEventData = {
  key: string
  title: string
  infoText: string
  reservationStartTime: string
  slots: CreateEventDataSlot[]
  registrationConfirmationMail: MailTemplate
  requestConfirmationMail: MailTemplate | null
  registrationFields: RegistrationField[]
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
  registrationFields: RegistrationField[] | null
}
