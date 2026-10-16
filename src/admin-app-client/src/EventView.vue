<script setup lang="ts">
import { ref, watch } from 'vue'
import * as DataTransfer from './DataTransfer'
import * as DateTime from './DateTime'
import { uiFetchAuthorized } from './UIFetch'
import ShareLink from './components/ShareLink.vue'

const props = defineProps<{
  event: DataTransfer.Event
  isNew?: boolean
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
  requestConfirmationMail: DataTransfer.MailTemplate | null
}
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
  isNew: boolean
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
watch(() => editEvent.value?.title, title => {
  if (!props.isNew || editEvent.value === undefined || title === undefined || isKeyChangedManually.value) return
  editEvent.value.key = slugify(title)
})
const onKeyInput = () => {
  if (editEvent.value === undefined) return
  isKeyChangedManually.value = editEvent.value.key !== ''
  if (!isKeyChangedManually.value) editEvent.value.key = slugify(editEvent.value.title)
}

let slotId = 0
const beginEdit = () => {
  editEvent.value = {
    key: props.event.key,
    title: props.event.title,
    infoText: props.event.infoText,
    reservationStartTime: props.event.reservationStartTime,
    slots: props.event.slots.map(slot => ({
      id: slotId++,
      original: slot,
      startTime: slot.startTime,
      hasDuration: slot.duration !== null,
      endTime: slot.duration ? DateTime.toInputString(DateTime.addTimeSpan(new Date(slot.startTime), slot.duration)) : undefined,
      hasClosingDate: slot.closingDate !== null,
      closingDate: slot.closingDate ? slot.closingDate : undefined,
      hasMaxQuantityPerBooking: slot.maxQuantityPerBooking !== null,
      maxQuantityPerBooking: slot.maxQuantityPerBooking || 0,
      hasRemainingCapacity: slot.remainingCapacity !== null,
      remainingCapacity: slot.remainingCapacity || 0,
      isNew: false,
      delete: false,
    })),
    registrationConfirmationMail: props.event.registrationConfirmationMail,
    requestConfirmationMail: props.event.requestConfirmationMail,
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

const isShowingDetails = ref(false)

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
    isNew: true,
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

const isValidTime = (v: string | undefined) => v !== undefined && v !== '' && !isNaN(new Date(v).getTime())

const validate = (edit: EditableEvent) => {
  if (props.isNew && edit.key.trim() === '') return 'Der Schlüssel darf nicht leer sein.'
  if (edit.title.trim() === '') return 'Der Titel darf nicht leer sein.'
  if (!isValidTime(edit.reservationStartTime)) return 'Der Registrierungsbeginn ist ungültig.'
  const slots = edit.slots.filter(v => !v.delete)
  for (const slot of slots) {
    if (!isValidTime(slot.startTime)) return 'Jeder Slot braucht eine gültige Startzeit.'
    if (slot.hasDuration && (!isValidTime(slot.endTime) || new Date(slot.endTime!).getTime() <= new Date(slot.startTime).getTime())) return 'Das Ende eines Slots muss nach dessen Start liegen.'
    if (slot.hasClosingDate && !isValidTime(slot.closingDate)) return 'Der Anmeldeschluss eines Slots ist ungültig.'
    if (slot.hasRemainingCapacity && (!Number.isInteger(slot.remainingCapacity) || slot.remainingCapacity < 0)) return 'Die freien Plätze müssen eine ganze Zahl ≥ 0 sein.'
    if (slot.hasMaxQuantityPerBooking && (!Number.isInteger(slot.maxQuantityPerBooking) || slot.maxQuantityPerBooking < 1)) return 'Die maximale Personenanzahl pro Registrierung muss eine ganze Zahl ≥ 1 sein.'
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
      canRequestIfFullyBooked: false,
    }]
  }
  if (slot.delete) {
    return [{ type: 'delete', startTime: original.startTime }]
  }
  if (!props.event.canEditSlot) return []

  const setTime = !DateTime.isSameTime(slot.startTime, original.startTime)
  const setDuration = getDurationMilliseconds(duration) !== getDurationMilliseconds(original.duration)
  const setClosingDate = closingDate === null || original.closingDate === null ? closingDate !== original.closingDate : !DateTime.isSameTime(closingDate, original.closingDate)
  const setMaxQuantityPerBooking = maxQuantityPerBooking !== original.maxQuantityPerBooking
  const setRemainingCapacity = remainingCapacity !== original.remainingCapacity
  if (!setTime && !setDuration && !setClosingDate && !setMaxQuantityPerBooking && !setRemainingCapacity) return []

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
    canRequestIfFullyBooked: null,
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
      canRequestIfFullyBooked: false,
    })),
    registrationConfirmationMail: edit.registrationConfirmationMail,
    requestConfirmationMail: edit.requestConfirmationMail,
  }
}

const getEventPatch = (edit: EditableEvent) : DataTransfer.PatchEventData => {
  const canEditEventData = props.event.canEditEventData
  return {
    key: null,
    title: canEditEventData && edit.title !== props.event.title ? edit.title : null,
    infoText: canEditEventData && edit.infoText !== props.event.infoText ? edit.infoText : null,
    reservationStartTime: canEditEventData && !DateTime.isSameTime(edit.reservationStartTime, props.event.reservationStartTime) ? edit.reservationStartTime : null,
    slots: edit.slots.flatMap(getSlotPatch),
    registrationConfirmationMail: null,
    requestConfirmationMail: null,
    setRequestConfirmationMail: false,
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
  else {
    saveError.value = 'Fehler beim Speichern des Events.'
  }
}

const isMarkedForDeletion = ref(false)
const deleteEvent = () => {
  if (!isMarkedForDeletion.value) {
    isMarkedForDeletion.value = true
    return
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
        <a v-if="event.canEditEventData || event.canAddSlot || event.canEditSlot || event.canDeleteSlot"
          class="p-2 cursor-pointer"
          title="Bearbeiten"
          @click="beginEdit">
          <i class="fa-solid fa-pencil"></i>
        </a>
        <a v-else class="p-2 cursor-pointer" title="Details anzeigen" @click="isShowingDetails = !isShowingDetails">
          <i class="fa-solid" :class="{ 'fa-eye': !isShowingDetails, 'fa-eye-slash': isShowingDetails }"></i>
        </a>
        <a v-if="event.canDelete" class="p-2 cursor-pointer" :class="{ 'text-rose-400': isMarkedForDeletion }" :title="isMarkedForDeletion ? 'Wirklich löschen' : 'Löschen'" @click="deleteEvent">
          <i class="fa-solid fa-trash-can"></i>
        </a>
        <ShareLink :url="event.publicRegistrationUrl" :title="event.title" />
        <a class="p-2 cursor-pointer" title="Duplizieren" @click="$emit('duplicateEvent')">
          <i class="fa-solid fa-clone"></i>
        </a>
      </span>
    </div>
    <span v-if="saveError !== undefined" class="text-rose-400">{{ saveError }}</span>
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
          <template v-if="editEvent === undefined || !event.canEditSlot">
            <li v-for="slot in event.slots" :key="JSON.stringify(slot)" class="border rounded p-4 flex flex-col">
              <div class="flex gap-2">
                <span class="grow rounded bg-blue-htlvb/50 -mx-2 px-2">{{ DateTime.format(new Date(slot.startTime)) }}</span>
                <a v-if="event.canDeleteSlot" class="p-2 cursor-pointer" title="Löschen">
                  <i class="fa-solid fa-trash-can"></i>
                </a>
              </div>
              <span class="text-sm">Ende: {{ slot.duration !== null ? DateTime.format(DateTime.addTimeSpan(new Date(slot.startTime), slot.duration)) : "keine Angabe" }}</span>
              <span class="text-sm">Anmeldeschluss: {{ slot.closingDate !== null ? DateTime.format(new Date(slot.closingDate)) : "-" }}</span>
              <span class="text-sm">Freie Plätze: {{ slot.remainingCapacity !== null ? slot.remainingCapacity : "unbegrenzt" }}</span>
              <span class="text-sm">Maximale Personen pro Registrierung: {{ slot.maxQuantityPerBooking !== null ? slot.maxQuantityPerBooking : "unbegrenzt" }}</span>
            </li>
          </template>
          <template v-if="editEvent !== undefined">
            <li v-for="slot in editEvent.slots.filter(v => event.canEditSlot || v.isNew)" :key="slot.id" class="border rounded p-4 flex flex-col gap-2" :class="{ 'bg-rose-400/10': slot.delete }">
              <div class="flex items-center gap-2">
                <input type="datetime-local" v-model="slot.startTime" class="grow input-text bg-blue-htlvb/50" />
                <a v-if="event.canDeleteSlot || slot.isNew" class="p-2 cursor-pointer" :class="{ 'text-rose-400': slot.delete }" title="Löschen" @click="deleteSlot(slot)">
                  <i class="fa-solid fa-trash-can"></i>
                </a>
              </div>
              <div class="flex flex-col gap-1">
                <label class="flex gap-2">
                  <input type="checkbox" v-model="slot.hasDuration" />
                  <span class="text-sm">Ende:</span>
                </label>
                <input type="datetime-local" v-model="slot.endTime" class="input-text" :disabled="!slot.hasDuration" />
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
    </div>
  </div>
</template>