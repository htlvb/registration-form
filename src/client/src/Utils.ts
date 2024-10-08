export module DateTime {
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
}
