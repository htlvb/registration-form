export type Event =
  { type: 'hidden' } & HiddenEvent
  | { type: 'released' } & ReleasedEvent
export type HiddenEvent = {
  title: string
  reservationStartTime: string
}
export type ReleasedEvent = {
  title: string
  infoText: string
  slots: Slot[]
}

export type Slot = {
  startTime: string
  duration: string | null
  type: SlotType
}

export type SlotType = {
  type: 'free'
  url: string
  closingDate: string | null
  maxQuantityPerBooking: number | null
  remainingCapacity: number | null
} | {
  type: 'closed'
} | {
  type: 'taken'
}

export type BookingResult = {
  slotType: SlotType
  mailSendError: boolean
}

export type BookingError =
  { error: 'capacity-exceeded', slotType: SlotType }
