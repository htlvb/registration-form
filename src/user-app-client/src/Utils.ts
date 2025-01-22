export namespace DateTime {
  export const formatDate = (v: Date, formatOptions?: Intl.DateTimeFormatOptions) => {
    const defaultOptions: Intl.DateTimeFormatOptions = {
      year: 'numeric',
      month: '2-digit',
      day: '2-digit'
    }
    const format = new Intl.DateTimeFormat('de-AT', { ...defaultOptions, ...formatOptions })
    return format.format(v)
  }

  export const formatTime = (v: Date) => {
    const format = new Intl.DateTimeFormat('de-AT', {
      hour: '2-digit',
      minute: '2-digit'
    })
    return format.format(v)
  }

  export const format = (v: Date, formatOptions?: Intl.DateTimeFormatOptions) => {
    const defaultOptions: Intl.DateTimeFormatOptions = {
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
      hour: '2-digit',
      minute: '2-digit',
    }
    const format = new Intl.DateTimeFormat('de-AT', { ...defaultOptions, ...formatOptions })
    return format.format(v)
  }

  export const getDate = (v: Date) => {
    return new Date(v.getFullYear(), v.getMonth(), v.getDate())
  }

  export const dateEquals = (a: Date, b: Date) => {
    return a.toDateString() === b.toDateString()
  }

  export const timeEquals = (a: Date, b: Date) => {
    return a.toTimeString() === b.toTimeString()
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
}

export namespace Text {
  export const pluralize = (v: number, singularText: string, pluralText: string) => {
    if (v === 1) {
      return `${v} ${singularText}`
    }
    return `${v} ${pluralText}`
  }
}
