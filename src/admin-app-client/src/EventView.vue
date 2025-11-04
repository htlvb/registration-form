<script setup lang="ts">
import { ref } from 'vue'
import * as DataTransfer from './DataTransfer'
import * as DateTime from './DateTime'

const props = defineProps<{
  event: DataTransfer.Event
}>()

defineEmits<{
  duplicateEvent: []
}>()

type EditableEvent = {
  title: string
  infoText: string
  reservationStartTime: string
  slots: {
    id: number
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
  }[]
  registrationConfirmationMail: DataTransfer.MailTemplate
  requestConfirmationMail: DataTransfer.MailTemplate
}

const editEvent = ref<EditableEvent>()

let slotId = 0
const beginEdit = () => {
  editEvent.value = {
    title: props.event.title,
    infoText: props.event.infoText,
    reservationStartTime: props.event.reservationStartTime,
    slots: props.event.slots.map(slot => ({
      id: slotId++,
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
    requestConfirmationMail: props.event.registrationConfirmationMail,
  }
}
const cancelEdit = () => {
  editEvent.value = undefined
  isMarkedForDeletion.value = false
}

const isShowingDetails = ref(false)

const addSlot = () => {
  if (editEvent.value === undefined) return
  
  editEvent.value.slots.push({
    id: slotId++,
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

const deleteSlot = (slot: EditableEvent['slots'][0]) => {
  if (slot.isNew) {
    if (editEvent.value === undefined) return
    editEvent.value.slots = editEvent.value.slots.filter(v => v !== slot)
  }
  else {
    slot.delete = !slot.delete
  }
}

const saveEvent = () => {

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
        <a class="p-2 cursor-pointer" title="Speichern" @click="saveEvent">
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
        <a class="p-2 cursor-pointer" title="Duplizieren" @click="$emit('duplicateEvent')">
          <i class="fa-solid fa-clone"></i>
        </a>
      </span>
    </div>
    <div v-if="isShowingDetails || editEvent !== undefined" class="flex flex-col gap-2">
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
        <ol class="flex flex-wrap gap-2">
          <template v-if="editEvent !== undefined && event.canEditSlot">
            <li v-for="slot in editEvent.slots" :key="slot.id" class="border rounded p-4 flex flex-col gap-2" :class="{ 'bg-rose-400/10': slot.delete }">
              <div class="flex items-center gap-2">
                <input type="datetime-local" v-model="slot.startTime" class="grow input-text" />
                <a v-if="event.canDeleteSlot" class="p-2 cursor-pointer" :class="{ 'text-rose-400': slot.delete }" title="Löschen" @click="deleteSlot(slot)">
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
          <template v-else>
            <li v-for="slot in event.slots" :key="JSON.stringify(slot)" class="border rounded p-4 flex flex-col">
              <div class="flex gap-2">
                <span class="grow">{{ DateTime.format(new Date(slot.startTime)) }}</span>
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
        </ol>
        <a v-if="event.canAddSlot" class="self-start button button-green" @click="addSlot">Slot hinzufügen</a>
      </div>
    </div>
  </div>
</template>