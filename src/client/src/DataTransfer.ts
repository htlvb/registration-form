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
  maxQuantity: number
} | {
  type: 'taken'
}
