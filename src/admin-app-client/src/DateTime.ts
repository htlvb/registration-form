export const format = (v: Date, formatOptions?: Intl.DateTimeFormatOptions) => {
  const defaultOptions: Intl.DateTimeFormatOptions = {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    weekday: 'short',
    hour: '2-digit',
    minute: '2-digit',
  }
  const format = new Intl.DateTimeFormat('de-AT', { ...defaultOptions, ...formatOptions })
  return format.format(v)
}

export const addTimeSpan = (date: Date, time: string) => {
  const [_all, daysText, hoursText, minutesText, secondsText, millisecondsText] = time.match(/^(?:(\d+)\.)?(\d+):(\d+):(\d+)(?:\.(\d+))?$/) || []
  const [days, hours, minutes, seconds, milliseconds] = [
    daysText === undefined ? 0 : parseInt(daysText),
    hoursText === undefined ? 0 : parseInt(hoursText),
    minutesText === undefined ? 0 : parseInt(minutesText),
    secondsText === undefined ? 0 : parseInt(secondsText),
    millisecondsText === undefined ? 0 : parseInt(millisecondsText),
  ]
  return new Date(date.getTime() + ((((days * 24 + hours) * 60) + minutes) * 60 + seconds) * 1000 + milliseconds)
}

const inputFormat = new Intl.DateTimeFormat('en-US', {
  year: 'numeric',
  month: '2-digit',
  day: '2-digit',
  hour: '2-digit',
  minute: '2-digit',
  second: '2-digit',
  hourCycle: 'h23',
})
export const toInputString = (date: Date) => {
  const parts = Object.fromEntries(inputFormat.formatToParts(date).map(v => [v.type, v.value]))
  return `${parts.year}-${parts.month}-${parts.day}T${parts.hour}:${parts.minute}:${parts.second}`
}

export const isSameTime = (a: string, b: string) => {
  return new Date(a).getTime() === new Date(b).getTime()
}

// time of day in UTC (no DST) of a duration since epoch is the duration's time part
const timeSpanFormat = new Intl.DateTimeFormat('en-US', {
  timeZone: 'UTC',
  hour: '2-digit',
  minute: '2-digit',
  second: '2-digit',
  hourCycle: 'h23',
})
export const toTimeSpan = (milliseconds: number) => {
  const days = Math.floor(milliseconds / (24 * 60 * 60 * 1000))
  return `${days > 0 ? `${days}.` : ''}${timeSpanFormat.format(milliseconds)}`
}
