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
  type: 'takenWithRequestPossible'
  url: string
  closingDate: string | null
  maxQuantityPerBooking: number | null
} | {
  type: 'taken'
} | {
  type: 'closed'
}

export type BookingResult = {
  slotType?: SlotType
  mailSendError: boolean
}

export type BookingError =
  | { error: 'event-not-found' }
  | { error: 'event-not-released' }
  | { error: 'slot-not-found' }
  | { error: 'slot-unavailable', slotType: SlotType }
  | { error: 'slot-needs-request', slotType: SlotType }
  | { error: 'invalid-subscription-quantity' }
  | { error: 'invalid-subscriber-name' }
  | { error: 'invalid-mail-address' }
  | { error: 'invalid-phone-number' }
  | { error: 'max-quantity-per-booking-exceeded' }
  | { error: 'capacity-exceeded', slotType: SlotType }
