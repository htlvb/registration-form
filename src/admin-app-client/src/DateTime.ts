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

export const toInputString = (date: Date) => {
  return date.toISOString().slice(0, 19) // 🤮
}
