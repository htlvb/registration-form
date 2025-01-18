<script lang="ts" setup>
import { computed, ref, watch } from 'vue'
import _ from 'lodash'
import type { Slot } from '@/DataTransfer'
import { DateTime, Text } from '@/Utils'

const props = defineProps<{
    slots: Slot[]
}>()

type SlotSummary = {
  hasUnlimitedCapacity: boolean
  freeSlots: number
  canRequestSlot: boolean
  hasTakenSlot: boolean
  hasClosedSlot: boolean
}

const summarizeSlots = (slots: Slot[]) => {
  return slots
    .reduce((slotSummary, slot) => {
      switch (slot.type.type) {
        case 'free':
          if (slot.type.remainingCapacity === null) {
            return { ...slotSummary, hasUnlimitedSlot: true }
          }
          else {
            return { ...slotSummary, freeSlots: slotSummary.freeSlots + slot.type.remainingCapacity }
          }
        case 'takenWithRequestPossible': return { ...slotSummary, canRequestSlot: true }
        case 'taken': return { ...slotSummary, hasTakenSlot: true }
        case 'closed': return { ...slotSummary, hasClosedSlot: true }
      }
    }, {
      hasUnlimitedCapacity: false,
      freeSlots: 0,
      canRequestSlot: false,
      hasTakenSlot: false,
      hasClosedSlot: false
    } as SlotSummary)
}

const slotSummaries = computed(() =>
  _(props.slots)
    .groupBy(v => new Date(v.startTime).toDateString())
    .map((v, k) => ({ date: k, slots: v, ...summarizeSlots(v) }))
    .value()
)

const selectedDate = ref<string>()
const selectedSlot = defineModel<Slot>()

watch(selectedDate, () => {
  if (selectedDate.value === undefined) {
    selectedSlot.value = undefined
    return
  }
  const selectedDateTime = new Date(selectedDate.value)
  const dateSlots = props.slots.filter(v => (v.type.type === 'free' || v.type.type === 'takenWithRequestPossible') && DateTime.dateEquals(selectedDateTime, new Date(v.startTime)))
  if (dateSlots.length === 1) {
    selectedSlot.value = dateSlots[0]
  }
  else if (selectedSlot.value !== undefined) {
    const selectedSlotTime = new Date(selectedSlot.value.startTime)
    selectedSlot.value = dateSlots.find(v => DateTime.timeEquals(selectedSlotTime, new Date(v.startTime)))
  }
})

watch(selectedSlot, () => {
  if (selectedSlot.value === undefined) {
    selectedDate.value = undefined
    return
  }
  selectedDate.value = DateTime.getDate(new Date(selectedSlot.value.startTime)).toDateString()
}, { immediate: true })

const selectedDateSlots = computed(() => {
  if (selectedDate.value === undefined) return []
  const date = new Date(selectedDate.value)
  return props.slots.filter(v => DateTime.dateEquals(date, new Date(v.startTime)))
})

const formatSlotTime = (slot: Slot) => {
  if (slot.duration !== null) {
    const startTime = new Date(slot.startTime)
    const endTime = DateTime.addTimeSpan(startTime, slot.duration)
    return `${DateTime.formatTime(startTime)} - ${DateTime.formatTime(endTime)}`
  }
  return DateTime.formatTime(new Date(slot.startTime))
}

const formatClosingDate = (v: Date) => {
  if (v.getHours() === 0 && v.getMinutes() === 0 && v.getSeconds() === 0 && v.getMilliseconds() === 0) {
    return DateTime.formatDate(new Date(v.getTime() - 1), { weekday: 'short' })
  }
  return DateTime.format(v, { weekday: 'short' })
}
</script>

<template>
  <div v-if="slotSummaries.length > 1">
    <h2 class="text-lg">Datum</h2>
    <div class="mt-2 flex flex-wrap gap-2">
      <button v-for="slotSummary in slotSummaries" :key="slotSummary.date"
        type="button"
        :disabled="slotSummary.freeSlots === 0 && !slotSummary.hasUnlimitedCapacity && !slotSummary.canRequestSlot"
        class="!flex flex-col items-center justify-center button"
        :class="{ 'button-htlvb-selected': selectedDate === slotSummary.date }"
        v-on="{ click: slotSummary.freeSlots > 0 || slotSummary.hasUnlimitedCapacity || slotSummary.canRequestSlot ? (() => selectedDate = slotSummary.date) : null }">
        <span>
          <span>{{ DateTime.formatDate(new Date(slotSummary.date), { weekday: 'short' }) }}</span>
          <span v-if="slotSummary.slots.length === 1">, {{ formatSlotTime(slotSummary.slots[0]) }}</span>
        </span>
        <template v-if="slotSummary.hasUnlimitedCapacity"></template>
        <span v-else-if="slotSummary.freeSlots > 0" class="text-sm">{{ Text.pluralize(slotSummary.freeSlots, 'freier Platz', 'freie Plätze') }}</span>
        <span v-else-if="slotSummary.hasTakenSlot || slotSummary.canRequestSlot" class="text-sm" :class="{ 'text-red-500': selectedDate === slotSummary.date }">ausgebucht</span>
        <span v-else-if="slotSummary.hasClosedSlot" class="text-sm">geschlossen</span>
      </button>
    </div>
  </div>
  <template v-if="selectedDateSlots.length > 1">
    <h2 class="text-lg mt-4">Uhrzeit</h2>
    <div class="mt-2 flex flex-wrap gap-2">
      <button v-for="slot in selectedDateSlots" :key="slot.startTime"
        type="button"
        :disabled="slot.type.type === 'taken' || slot.type.type === 'closed'"
        class="!flex flex-col items-center justify-center button"
        :class="{ 'button-htlvb-selected': selectedSlot === slot }"
        v-on="{ click: slot.type.type === 'free' || slot.type.type === 'takenWithRequestPossible' ? (() => selectedSlot = slot) : null }">
        <span>{{ formatSlotTime(slot) }}</span>
        <template v-if="slot.type.type === 'free'">
          <span v-if="slot.type.remainingCapacity != null" class="text-sm">{{ Text.pluralize(slot.type.remainingCapacity, 'freier Platz', 'freie Plätze') }}</span>
        </template>
        <span v-else-if="slot.type.type === 'taken' || slot.type.type === 'takenWithRequestPossible'" class="text-sm" :class="{ 'text-red-500': selectedSlot === slot }">ausgebucht</span>
        <span v-else-if="slot.type.type === 'closed'" class="text-sm">geschlossen</span>
      </button>
    </div>
  </template>
  <div v-if="selectedSlot !== undefined && (selectedSlot.type.type === 'free' || selectedSlot.type.type === 'takenWithRequestPossible') && selectedSlot.type.closingDate !== null" class="mt-2">
    <span class="text-sm">Anmeldeschluss: {{ formatClosingDate(new Date(selectedSlot.type.closingDate)) }}</span>
  </div>
</template>