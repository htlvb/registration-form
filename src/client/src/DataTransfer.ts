export type Schedule =
  { type: 'hidden' } & HiddenSchedule
  | { type: 'released' } & ReleasedSchedule
export type HiddenSchedule = {
  title: string
  reservationStartTime: string
}
export type ReleasedSchedule = {
  title: string
  infoText: string
  entries: ScheduleEntry[]
}

export type ScheduleEntry = {
  startTime: string
  reservationType: ReservationType
}

export type ReservationType = {
  type: 'free',
  url: string,
  maxQuantityPerBooking: number | null
  remainingCapacity: number | null
} | {
  type: 'taken'
}

export type BookingResult = {
  reservationType: ReservationType
  mailSendError: boolean
}

export type BookingError =
  { error: 'capacity-exceeded', reservationType: ReservationType }
