<script setup lang="ts">
import { ref, watch } from 'vue'
import * as DataTransfer from './DataTransfer'
import * as DateTime from './DateTime'
import { uiFetchAuthorized } from './UIFetch'
import ShareLink from './components/ShareLink.vue'
import SlotRegistrations from './SlotRegistrations.vue'

const props = defineProps<{
  event: DataTransfer.Event
  isNew?: boolean
  canDuplicate?: boolean
}>()

const emit = defineEmits<{
  duplicateEvent: []
  eventUpdated: []
  editCancelled: []
}>()

type EditableEvent = {
  key: string
  title: string
  infoText: string
  reservationStartTime: string
  slots: EditableSlot[]
  registrationConfirmationMail: DataTransfer.MailTemplate
  requestConfirmationMail: DataTransfer.MailTemplate // only used if a slot allows requests
  registrationFields: EditableRegistrationField[]
}
type EditableRegistrationField = DataTransfer.RegistrationField & { id: number }
type EditableSlot = {
  id: number
  original: DataTransfer.Slot | undefined
  startTime: string
  hasDuration: boolean
  endTime: string | undefined
  hasClosingDate: boolean
  closingDate: string | undefined
  hasMaxQuantityPerBooking: boolean
  maxQuantityPerBooking: number
  hasRemainingCapacity: boolean
  remainingCapacity: number
  canRequestIfFullyBooked: boolean
  isNew: boolean
  canEdit: DataTransfer.Slot['canEdit']
  delete: boolean
}

const editEvent = ref<EditableEvent>()

const slugify = (text: string) =>
  text
    .toLowerCase()
    .replace(/ä/g, 'ae').replace(/ö/g, 'oe').replace(/ü/g, 'ue').replace(/ß/g, 'ss')
    .normalize('NFD').replace(/\p{Diacritic}/gu, '')
    .replace(/['’]/g, '') // "Let's" -> "lets"
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-|-$/g, '')

// key of a new event follows the title until it's changed manually (clearing it switches back to following the title)
const isKeyChangedManually = ref(false)
const onKeyInput = () => {
  if (editEvent.value === undefined) return
  isKeyChangedManually.value = editEvent.value.key !== ''
  if (!isKeyChangedManually.value) editEvent.value.key = slugify(editEvent.value.title)
}

let slotId = 0
let registrationFieldId = 0
const beginEdit = () => {
  editEvent.value = {
    key: props.event.key,
    title: props.event.title,
    infoText: props.event.infoText,
    reservationStartTime: props.event.reservationStartTime,
    slots: props.event.slots.map(slot => ({
      id: slotId++,
      original: props.isNew ? undefined : slot, // slots of a new event (e.g. copied from a duplicated event) don't exist yet
      startTime: slot.startTime,
      hasDuration: slot.duration !== null,
      endTime: slot.duration ? DateTime.toInputString(DateTime.addTimeSpan(new Date(slot.startTime), slot.duration)) : undefined,
      hasClosingDate: slot.closingDate !== null,
      closingDate: slot.closingDate ? slot.closingDate : undefined,
      hasMaxQuantityPerBooking: slot.maxQuantityPerBooking !== null,
      maxQuantityPerBooking: slot.maxQuantityPerBooking || 0,
      hasRemainingCapacity: slot.remainingCapacity !== null,
      remainingCapacity: slot.remainingCapacity || 0,
      canRequestIfFullyBooked: slot.canRequestIfFullyBooked,
      isNew: props.isNew,
      canEdit: props.isNew ? 'open' : slot.canEdit,
      delete: false,
    })),
    // copies, so that editing doesn't change the event
    registrationConfirmationMail: { ...props.event.registrationConfirmationMail },
    requestConfirmationMail: props.event.requestConfirmationMail !== null ? { ...props.event.requestConfirmationMail } : { subject: '', contentTemplate: '' },
    registrationFields: props.event.registrationFields.map(v => ({ ...v, id: registrationFieldId++ })),
  }
}
const cancelEdit = () => {
  editEvent.value = undefined
  saveError.value = undefined
  isMarkedForDeletion.value = false
  emit('editCancelled')
}
// a new event exists only while it's being edited
if (props.isNew) beginEdit()
// after `beginEdit`, so that the key of a duplicated event is calculated right away
watch(() => editEvent.value?.title, title => {
  if (!props.isNew || editEvent.value === undefined || title === undefined || isKeyChangedManually.value) return
  editEvent.value.key = slugify(title)
}, { immediate: true })

const isShowingDetails = ref(false)
const isShowingRegistrations = ref(false)

const addSlot = () => {
  if (editEvent.value === undefined) return

  editEvent.value.slots.push({
    id: slotId++,
    original: undefined,
    startTime: '',
    hasDuration: false,
    endTime: undefined,
    hasClosingDate: false,
    closingDate: undefined,
    hasMaxQuantityPerBooking: false,
    maxQuantityPerBooking: 1,
    hasRemainingCapacity: false,
    remainingCapacity: 0,
    canRequestIfFullyBooked: false,
    isNew: true,
    canEdit: 'open',
    delete: false,
  })
}

const deleteSlot = (slot: EditableSlot) => {
  if (slot.isNew) {
    if (editEvent.value === undefined) return
    editEvent.value.slots = editEvent.value.slots.filter(v => v !== slot)
  }
  else {
    slot.delete = !slot.delete
  }
}

// slots with unlimited capacity are never fully booked
const getCanRequestIfFullyBooked = (slot: EditableSlot) => slot.hasRemainingCapacity && slot.canRequestIfFullyBooked

// request confirmation mail is needed iff a slot allows requests
const needsRequestConfirmationMail = (edit: EditableEvent) => edit.slots.some(v => !v.delete && getCanRequestIfFullyBooked(v))
const getRequestConfirmationMail = (edit: EditableEvent) => needsRequestConfirmationMail(edit) ? edit.requestConfirmationMail : null

const registrationFieldTypes: { value: DataTransfer.RegistrationField['type'], label: string }[] = [
  { value: 'text', label: 'Text' },
  { value: 'mail', label: 'E-Mail-Adresse' },
  { value: 'phone', label: 'Telefonnummer' },
]
const registrationFieldValidators: { value: DataTransfer.RegistrationField['validator'], label: string }[] = [
  { value: 'none', label: 'Optional' },
  { value: 'notEmpty', label: 'Pflichtfeld' },
]
const addRegistrationField = () => {
  if (editEvent.value === undefined) return
  editEvent.value.registrationFields.push({ id: registrationFieldId++, key: '', label: '', type: 'text', validator: 'none' })
}
const deleteRegistrationField = (field: EditableRegistrationField) => {
  if (editEvent.value === undefined) return
  editEvent.value.registrationFields = editEvent.value.registrationFields.filter(v => v !== field)
}
const moveRegistrationFieldUp = (index: number) => {
  if (editEvent.value === undefined || index <= 0) return
  const fields = editEvent.value.registrationFields
  fields.splice(index - 1, 2, fields[index]!, fields[index - 1]!)
}
const getRegistrationFields = (edit: EditableEvent) : DataTransfer.RegistrationField[] =>
  edit.registrationFields.map(v => ({ key: v.key.trim(), label: v.label.trim(), type: v.type, validator: v.validator }))
const isSameRegistrationFieldList = (a: DataTransfer.RegistrationField[], b: DataTransfer.RegistrationField[]) =>
  a.length === b.length && a.every((v, i) => v.key === b[i]!.key && v.label === b[i]!.label && v.type === b[i]!.type && v.validator === b[i]!.validator)
const getPlaceholder = (key: string) => `{{{${key}}}}`

const isValidTime = (v: string | undefined) => v !== undefined && v !== '' && !isNaN(new Date(v).getTime())

const validate = (edit: EditableEvent) => {
  if (props.isNew && edit.key.trim() === '') return 'Der Schlüssel darf nicht leer sein.'
  if (edit.title.trim() === '') return 'Der Titel darf nicht leer sein.'
  if (!isValidTime(edit.reservationStartTime)) return 'Der Registrierungsbeginn ist ungültig.'
  const slots = edit.slots.filter(v => !v.delete)
  for (const slot of slots) {
    if (!isValidTime(slot.startTime)) return 'Jeder Slot braucht eine gültige Startzeit.'
    if (slot.isNew && new Date(slot.startTime).getTime() <= Date.now()) return 'Neue Slots müssen in der Zukunft liegen.'
    if (slot.hasDuration && (!isValidTime(slot.endTime) || new Date(slot.endTime!).getTime() <= new Date(slot.startTime).getTime())) return 'Das Ende eines Slots muss nach dessen Start liegen.'
    if (slot.hasClosingDate && !isValidTime(slot.closingDate)) return 'Der Anmeldeschluss eines Slots ist ungültig.'
    if (slot.hasRemainingCapacity && (!Number.isInteger(slot.remainingCapacity) || slot.remainingCapacity < 0)) return 'Die freien Plätze müssen eine ganze Zahl ≥ 0 sein.'
    if (slot.hasMaxQuantityPerBooking && (!Number.isInteger(slot.maxQuantityPerBooking) || slot.maxQuantityPerBooking < 1)) return 'Die maximale Personenanzahl pro Registrierung muss eine ganze Zahl ≥ 1 sein.'
  }
  const registrationFields = getRegistrationFields(edit)
  for (const field of registrationFields) {
    if (!/^[A-Za-z][A-Za-z0-9_]*$/.test(field.key)) return `Der Schlüssel "${field.key}" eines Registrierungsfelds ist ungültig (nur Buchstaben, Ziffern und _, beginnend mit einem Buchstaben).`
    if (field.key === 'Date' || field.key === 'Time') return `Der Schlüssel "${field.key}" ist für Mailvorlagen reserviert.`
    if (field.label === '') return 'Jedes Registrierungsfeld braucht eine Bezeichnung.'
  }
  if (new Set(registrationFields.map(v => v.key)).size !== registrationFields.length) return 'Zwei Registrierungsfelder dürfen nicht denselben Schlüssel haben.'
  if (registrationFields.find(v => v.type === 'mail')?.validator !== 'notEmpty') return 'Das erste Registrierungsfeld vom Typ E-Mail-Adresse muss ein Pflichtfeld sein, weil Bestätigungen dorthin gesendet werden.'
  if (edit.registrationConfirmationMail.subject.trim() === '') return 'Der Betreff der Registrierungsbestätigung darf nicht leer sein.'
  if (needsRequestConfirmationMail(edit)) {
    if (edit.requestConfirmationMail.subject.trim() === '') return 'Der Betreff der Anfragebestätigung darf nicht leer sein.'
    if (edit.requestConfirmationMail.contentTemplate.trim() === '') return 'Der Inhalt der Anfragebestätigung darf nicht leer sein.'
  }
  const startTimes = slots.map(v => new Date(v.startTime).getTime())
  if (new Set(startTimes).size !== startTimes.length) return 'Zwei Slots dürfen nicht zur selben Zeit beginnen.'
}

const getDuration = (slot: EditableSlot) => {
  if (!slot.hasDuration || slot.endTime === undefined) return null
  return DateTime.toTimeSpan(new Date(slot.endTime).getTime() - new Date(slot.startTime).getTime())
}
const getDurationMilliseconds = (duration: string | null) => {
  if (duration === null) return null
  return DateTime.addTimeSpan(new Date(0), duration).getTime()
}

const getSlotPatch = (slot: EditableSlot) : DataTransfer.PatchEventDataSlot[] => {
  const duration = getDuration(slot)
  const closingDate = slot.hasClosingDate && slot.closingDate !== undefined ? slot.closingDate : null
  const maxQuantityPerBooking = slot.hasMaxQuantityPerBooking ? slot.maxQuantityPerBooking : null
  const remainingCapacity = slot.hasRemainingCapacity ? slot.remainingCapacity : null

  const original = slot.original
  if (original === undefined) {
    return [{
      type: 'create',
      startTime: slot.startTime,
      duration,
      closingDate,
      maxQuantityPerBooking,
      remainingCapacity,
      canRequestIfFullyBooked: getCanRequestIfFullyBooked(slot),
    }]
  }
  if (slot.delete) {
    return [{ type: 'delete', startTime: original.startTime }]
  }
  if (slot.canEdit === 'locked') return []

  const setTime = slot.canEdit === 'open' && !DateTime.isSameTime(slot.startTime, original.startTime)
  const setDuration = slot.canEdit === 'open' && getDurationMilliseconds(duration) !== getDurationMilliseconds(original.duration)
  const setClosingDate = closingDate === null || original.closingDate === null ? closingDate !== original.closingDate : !DateTime.isSameTime(closingDate, original.closingDate)
  const setMaxQuantityPerBooking = maxQuantityPerBooking !== original.maxQuantityPerBooking
  const setRemainingCapacity = remainingCapacity !== original.remainingCapacity
  const canRequestIfFullyBooked = getCanRequestIfFullyBooked(slot)
  const setCanRequestIfFullyBooked = canRequestIfFullyBooked !== original.canRequestIfFullyBooked
  if (!setTime && !setDuration && !setClosingDate && !setMaxQuantityPerBooking && !setRemainingCapacity && !setCanRequestIfFullyBooked) return []

  return [{
    type: 'update',
    startTime: original.startTime,
    newTime: setTime ? slot.startTime : null,
    duration,
    setDuration,
    closingDate,
    setClosingDate,
    maxQuantityPerBooking,
    setMaxQuantityPerBooking,
    remainingCapacity,
    setRemainingCapacity,
    canRequestIfFullyBooked: setCanRequestIfFullyBooked ? canRequestIfFullyBooked : null,
  }]
}

const getNewEvent = (edit: EditableEvent) : DataTransfer.CreateEventData => {
  return {
    key: edit.key.trim(),
    title: edit.title,
    infoText: edit.infoText,
    reservationStartTime: edit.reservationStartTime,
    slots: edit.slots.map(slot => ({
      startTime: slot.startTime,
      duration: getDuration(slot),
      closingDate: slot.hasClosingDate && slot.closingDate !== undefined ? slot.closingDate : null,
      maxQuantityPerBooking: slot.hasMaxQuantityPerBooking ? slot.maxQuantityPerBooking : null,
      remainingCapacity: slot.hasRemainingCapacity ? slot.remainingCapacity : null,
      canRequestIfFullyBooked: getCanRequestIfFullyBooked(slot),
    })),
    registrationConfirmationMail: edit.registrationConfirmationMail,
    requestConfirmationMail: getRequestConfirmationMail(edit),
    registrationFields: getRegistrationFields(edit),
  }
}

const isSameMailTemplate = (a: DataTransfer.MailTemplate | null, b: DataTransfer.MailTemplate | null) => {
  if (a === null || b === null) return a === b
  return a.subject === b.subject && a.contentTemplate === b.contentTemplate
}

const getEventPatch = (edit: EditableEvent) : DataTransfer.PatchEventData => {
  const canEditEventData = props.event.canEditEventData
  const requestConfirmationMail = getRequestConfirmationMail(edit)
  const setRequestConfirmationMail = canEditEventData && !isSameMailTemplate(requestConfirmationMail, props.event.requestConfirmationMail)
  const registrationFields = getRegistrationFields(edit)
  return {
    key: null,
    title: canEditEventData && edit.title !== props.event.title ? edit.title : null,
    infoText: canEditEventData && edit.infoText !== props.event.infoText ? edit.infoText : null,
    reservationStartTime: canEditEventData && !DateTime.isSameTime(edit.reservationStartTime, props.event.reservationStartTime) ? edit.reservationStartTime : null,
    slots: edit.slots.flatMap(getSlotPatch),
    registrationConfirmationMail: canEditEventData && !isSameMailTemplate(edit.registrationConfirmationMail, props.event.registrationConfirmationMail) ? edit.registrationConfirmationMail : null,
    requestConfirmationMail: setRequestConfirmationMail ? requestConfirmationMail : null,
    setRequestConfirmationMail,
    registrationFields: props.event.canEditRegistrationFields && !isSameRegistrationFieldList(registrationFields, props.event.registrationFields) ? registrationFields : null,
  }
}

const isSaving = ref(false)
const hasSavingFailed = ref(false)
const saveError = ref<string>()
const saveEvent = async () => {
  if (editEvent.value === undefined || isSaving.value) return

  saveError.value = validate(editEvent.value)
  if (saveError.value !== undefined) return

  // url of a new event is the url to create it
  const result = await uiFetchAuthorized(isSaving, hasSavingFailed, props.event.url, {
    method: props.isNew ? 'POST' : 'PATCH',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(props.isNew ? getNewEvent(editEvent.value) : getEventPatch(editEvent.value)),
  })
  if (result.succeeded) {
    editEvent.value = undefined
    emit('eventUpdated')
  }
  else if (result.response?.status === 409) {
    saveError.value = 'Ein Event mit diesem Schlüssel existiert bereits.'
  }
  else if (props.isNew && result.response?.status === 403) {
    saveError.value = 'Das Event kann nicht erstellt werden, weil Ihr Konto keine E-Mail-Adresse hat.'
  }
  else if (result.response?.status === 403) {
    saveError.value = 'Sie sind nicht berechtigt, dieses Event zu bearbeiten.'
  }
  else {
    saveError.value = 'Fehler beim Speichern des Events.'
  }
}

const isMarkedForDeletion = ref(false)
const isDeleting = ref(false)
const hasDeletingFailed = ref(false)
const deleteError = ref<string>()
const deleteEvent = async () => {
  if (isDeleting.value) return
  if (!isMarkedForDeletion.value) {
    isMarkedForDeletion.value = true
    return
  }

  deleteError.value = undefined
  const result = await uiFetchAuthorized(isDeleting, hasDeletingFailed, props.event.url, { method: 'DELETE' })
  if (result.succeeded) {
    emit('eventUpdated')
  }
  else {
    isMarkedForDeletion.value = false
    deleteError.value = 'Fehler beim Löschen des Events.'
  }
}
</script>
<template>
  <div class="flex flex-col gap-2" :class="{ '[&:not(:last-child)]:pb-2 [&:not(:last-child)]:border-b': isShowingDetails }">
    <div class="flex items-center gap-2">
      <input v-if="editEvent !== undefined" type="text" v-model="editEvent.title" class="input-text" />
      <span v-else>{{ event.title }}</span>
      <span v-if="editEvent !== undefined">
        <a class="p-2 cursor-pointer" :class="{ 'opacity-50 cursor-wait!': isSaving }" title="Speichern" @click="saveEvent">
          <i class="fa-solid fa-floppy-disk"></i>
        </a>
        <a class="p-2 cursor-pointer" title="Abbrechen" @click="cancelEdit">
          <i class="fa-solid fa-arrow-rotate-left"></i>
        </a>
      </span>
      <span v-else>
        <a v-if="event.canEditEventData || event.canAddSlot || event.slots.some(v => v.canEdit !== 'locked') || event.canDeleteSlot"
          class="p-2 cursor-pointer"
          title="Bearbeiten"
          @click="beginEdit">
          <i class="fa-solid fa-pencil"></i>
        </a>
        <a v-else class="p-2 cursor-pointer" title="Details anzeigen" @click="isShowingDetails = !isShowingDetails">
          <i class="fa-solid" :class="{ 'fa-eye': !isShowingDetails, 'fa-eye-slash': isShowingDetails }"></i>
        </a>
        <a v-if="event.canDelete" class="p-2 cursor-pointer" :class="{ 'text-rose-400': isMarkedForDeletion, 'opacity-50 cursor-wait!': isDeleting }" :title="isMarkedForDeletion ? 'Wirklich löschen' : 'Löschen'" @click="deleteEvent">
          <i class="fa-solid fa-trash-can"></i>
        </a>
        <a v-if="event.canViewRegistrations" class="p-2 cursor-pointer" :class="{ 'text-blue-400': isShowingRegistrations }" :title="isShowingRegistrations ? 'Registrierungen ausblenden' : 'Registrierungen anzeigen'" @click="isShowingRegistrations = !isShowingRegistrations">
          <i class="fa-solid fa-users"></i>
        </a>
        <ShareLink :url="event.publicRegistrationUrl" :title="event.title" />
        <a v-if="canDuplicate" class="p-2 cursor-pointer" title="Duplizieren" @click="$emit('duplicateEvent')">
          <i class="fa-solid fa-clone"></i>
        </a>
      </span>
    </div>
    <span v-if="saveError !== undefined" class="text-rose-400">{{ saveError }}</span>
    <span v-if="deleteError !== undefined" class="text-rose-400">{{ deleteError }}</span>
    <div v-if="isShowingDetails || editEvent !== undefined" class="flex flex-col gap-2">
      <div v-if="isNew && editEvent !== undefined" class="flex flex-col gap-1">
        <span class="text-sm">Schlüssel (Teil der Registrierungs-URL)</span>
        <input type="text" v-model="editEvent.key" class="input-text self-start" @input="onKeyInput" />
      </div>
      <div class="flex flex-col gap-1">
        <span class="text-sm">Infotext</span>
        <textarea v-if="editEvent !== undefined" v-model="editEvent.infoText" class="input-text"></textarea>
        <span v-else class="ml-4">{{ event.infoText }}</span>
      </div>
      <div class="flex flex-col gap-1">
        <span class="text-sm">Registrierung möglich ab</span>
        <input v-if="editEvent !== undefined" type="datetime-local" v-model="editEvent.reservationStartTime" class="input-text self-start" />
        <span v-else class="ml-4">{{ DateTime.format(new Date(event.reservationStartTime)) }}</span>
      </div>
      <div class="flex flex-col gap-1">
        <span class="text-sm">Slots</span>
        <ol class="flex flex-wrap gap-2 items-stretch">
          <!-- slots that can't be edited are shown read-only while editing -->
          <template v-for="slot in event.slots" :key="JSON.stringify(slot)">
            <li v-if="editEvent === undefined || (!isNew && slot.canEdit === 'locked')" class="border rounded p-4 flex flex-col">
              <div class="flex gap-2">
                <span class="grow rounded bg-blue-htlvb/50 -mx-2 px-2">{{ DateTime.format(new Date(slot.startTime)) }}</span>
                <a v-if="event.canDeleteSlot" class="p-2 cursor-pointer" title="Löschen">
                  <i class="fa-solid fa-trash-can"></i>
                </a>
              </div>
              <span class="text-sm">Ende: {{ slot.duration !== null ? DateTime.format(DateTime.addTimeSpan(new Date(slot.startTime), slot.duration)) : "keine Angabe" }}</span>
              <span class="text-sm">Anmeldeschluss: {{ slot.closingDate !== null ? DateTime.format(new Date(slot.closingDate)) : "-" }}</span>
              <span class="text-sm">Freie Plätze: {{ slot.remainingCapacity !== null ? slot.remainingCapacity : "unbegrenzt" }}</span>
              <span v-if="slot.remainingCapacity !== null" class="text-sm">Anfragen wenn ausgebucht: {{ slot.canRequestIfFullyBooked ? "ja" : "nein" }}</span>
              <span class="text-sm">Maximale Personen pro Registrierung: {{ slot.maxQuantityPerBooking !== null ? slot.maxQuantityPerBooking : "unbegrenzt" }}</span>
            </li>
          </template>
          <template v-if="editEvent !== undefined">
            <li v-for="slot in editEvent.slots.filter(v => v.canEdit !== 'locked')" :key="slot.id" class="border rounded p-4 flex flex-col gap-2" :class="{ 'bg-rose-400/10': slot.delete }">
              <div class="flex items-center gap-2">
                <input type="datetime-local" v-model="slot.startTime" class="grow input-text bg-blue-htlvb/50" :disabled="slot.canEdit === 'timesLocked'" :title="slot.canEdit !== 'timesLocked' ? undefined : 'Die Zeit kann nicht mehr geändert werden, weil die Registrierung bereits begonnen hat.'" />
                <a v-if="event.canDeleteSlot || slot.isNew" class="p-2 cursor-pointer" :class="{ 'text-rose-400': slot.delete }" title="Löschen" @click="deleteSlot(slot)">
                  <i class="fa-solid fa-trash-can"></i>
                </a>
              </div>
              <div class="flex flex-col gap-1">
                <label class="flex gap-2">
                  <input type="checkbox" v-model="slot.hasDuration" :disabled="slot.canEdit === 'timesLocked'" />
                  <span class="text-sm">Ende:</span>
                </label>
                <input type="datetime-local" v-model="slot.endTime" class="input-text" :disabled="!slot.hasDuration || slot.canEdit === 'timesLocked'" />
              </div>
              <div class="flex flex-col gap-1">
                <label class="flex gap-2">
                  <input type="checkbox" v-model="slot.hasClosingDate" />
                  <span class="text-sm">Anmeldeschluss:</span>
                </label>
                <input type="datetime-local" v-model="slot.closingDate" class="input-text" :disabled="!slot.hasClosingDate" />
              </div>
              <div class="flex flex-col gap-1">
                <label class="flex gap-2">
                  <input type="checkbox" v-model="slot.hasRemainingCapacity" />
                  <span class="text-sm">Freie Plätze:</span>
                </label>
                <input type="number" v-model="slot.remainingCapacity" class="input-text" :disabled="!slot.hasRemainingCapacity" min="0" />
              </div>
              <!-- not bound with v-model, so that it's shown unchecked, but remembered while capacity is unlimited -->
              <label class="flex gap-2">
                <input type="checkbox" :checked="getCanRequestIfFullyBooked(slot)" :disabled="!slot.hasRemainingCapacity" @change="slot.canRequestIfFullyBooked = ($event.target as HTMLInputElement).checked" />
                <span class="text-sm">Anfragen möglich, wenn ausgebucht</span>
              </label>
              <div class="flex flex-col gap-1">
                <label class="flex gap-2">
                  <input type="checkbox" v-model="slot.hasMaxQuantityPerBooking" />
                  <span class="text-sm">Maximale Personen pro Registrierung:</span>
                </label>
                <input type="number" v-model="slot.maxQuantityPerBooking" class="input-text" :disabled="!slot.hasMaxQuantityPerBooking" min="1" />
              </div>
            </li>
          </template>
          <li v-if="editEvent !== undefined && event.canAddSlot" class="flex items-center">
            <a class="content-center aspect-square button" @click="addSlot">
              <i class="fa-solid fa-plus"></i>
            </a>
          </li>
        </ol>
      </div>
      <div class="flex flex-col gap-1">
        <span class="text-sm">Registrierungsfelder</span>
        <table v-if="editEvent !== undefined && event.canEditRegistrationFields" class="text-sm text-left">
          <thead>
            <tr>
              <th class="font-semibold px-1">Schlüssel</th>
              <th class="font-semibold px-1">Bezeichnung</th>
              <th class="font-semibold px-1">Typ</th>
              <th class="font-semibold px-1">Prüfung</th>
              <th class="px-1"><span class="sr-only">Aktionen</span></th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="(field, index) in editEvent.registrationFields" :key="field.id">
              <td class="p-1"><input type="text" v-model="field.key" class="input-text w-full" /></td>
              <td class="p-1"><input type="text" v-model="field.label" class="input-text w-full" /></td>
              <td class="p-1">
                <select v-model="field.type" class="input-text w-full">
                  <option v-for="fieldType in registrationFieldTypes" :key="fieldType.value" :value="fieldType.value">{{ fieldType.label }}</option>
                </select>
              </td>
              <td class="p-1">
                <select v-model="field.validator" class="input-text w-full">
                  <option v-for="validator in registrationFieldValidators" :key="validator.value" :value="validator.value">{{ validator.label }}</option>
                </select>
              </td>
              <td class="p-1 whitespace-nowrap">
                <a v-if="index > 0" class="p-2 cursor-pointer" title="Nach oben" @click="moveRegistrationFieldUp(index)">
                  <i class="fa-solid fa-arrow-up"></i>
                </a>
                <a class="p-2 cursor-pointer" title="Löschen" @click="deleteRegistrationField(field)">
                  <i class="fa-solid fa-trash-can"></i>
                </a>
              </td>
            </tr>
          </tbody>
          <tfoot>
            <tr>
              <td colspan="5" class="p-1">
                <a class="button" @click="addRegistrationField">
                  <i class="fa-solid fa-plus"></i>
                </a>
              </td>
            </tr>
          </tfoot>
        </table>
        <ul v-else class="ml-4">
          <li v-for="field in event.registrationFields" :key="field.key">
            {{ field.label }}
            <span class="text-sm">({{ registrationFieldTypes.find(v => v.value === field.type)?.label }}, {{ registrationFieldValidators.find(v => v.value === field.validator)?.label }}, <code v-text="getPlaceholder(field.key)"></code>)</span>
          </li>
        </ul>
      </div>
      <template v-if="editEvent !== undefined">
        <div class="flex flex-col gap-1">
          <span class="text-sm">Registrierungsbestätigung</span>
          <input type="text" v-model="editEvent.registrationConfirmationMail.subject" class="input-text" placeholder="Betreff" :disabled="!event.canEditEventData" />
          <textarea v-model="editEvent.registrationConfirmationMail.contentTemplate" class="input-text" rows="6" placeholder="Inhalt" :disabled="!event.canEditEventData"></textarea>
        </div>
        <div v-if="needsRequestConfirmationMail(editEvent)" class="flex flex-col gap-1">
          <span class="text-sm">Anfragebestätigung (nötig, weil Anfragen möglich sind)</span>
          <input type="text" v-model="editEvent.requestConfirmationMail.subject" class="input-text" placeholder="Betreff" :disabled="!event.canEditEventData" />
          <textarea v-model="editEvent.requestConfirmationMail.contentTemplate" class="input-text" rows="6" placeholder="Inhalt" :disabled="!event.canEditEventData"></textarea>
        </div>
        <span class="text-sm">
          Platzhalter im Inhalt:
          <template v-for="key in [...editEvent.registrationFields.map(v => v.key.trim()).filter(v => v !== ''), 'Date', 'Time']" :key="key"><code v-text="getPlaceholder(key)"></code>&#32;</template>
        </span>
      </template>
    </div>
    <div v-if="isShowingRegistrations && editEvent === undefined" class="flex flex-col gap-2">
      <span class="text-sm">Registrierungen</span>
      <span v-if="event.slots.length === 0" class="ml-4">Keine Slots vorhanden.</span>
      <SlotRegistrations v-for="slot in event.slots" :key="slot.startTime" :event-slot="slot" :registration-fields="event.registrationFields" @registration-cancelled="$emit('eventUpdated')" />
    </div>
  </div>
</template>