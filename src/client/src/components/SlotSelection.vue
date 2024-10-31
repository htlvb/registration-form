<script lang="ts" setup>
import { computed, ref, watch } from 'vue'
import _ from 'lodash'
import type { Slot } from '@/DataTransfer'
import { DateTime } from '@/Utils'

const props = defineProps<{
    slots: Slot[]
}>()

type SlotSummary = {
  hasUnlimitedCapacity: boolean
  freeSlots: number
  hasClosedSlot: boolean
  hasTakenSlot: boolean
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
        case 'closed': return { ...slotSummary, hasClosedSlot: true }
        case 'taken': return { ...slotSummary, hasTakenSlot: true }
      }
    }, {
      hasUnlimitedCapacity: false,
      freeSlots: 0,
      hasClosedSlot: false,
      hasTakenSlot: false
    } as SlotSummary)
}

const slotSummaries = computed(() =>
  _(props.slots)
    .groupBy(v => new Date(v.startTime).toDateString())
    .map((v, k) => ({ date: k, ...summarizeSlots(v) }))
    .value()
)

const selectedDate = ref<string>()
const selectedSlot = defineModel<Slot>()

watch(selectedDate, () => {
  if (selectedSlot.value === undefined) return
  if (selectedDate.value === undefined) {
    selectedSlot.value = undefined
    return
  }
  const selectedDateTime = new Date(selectedDate.value)
  const selectedSlotTime = new Date(selectedSlot.value.startTime)

  selectedSlot.value = props.slots
    .find(v =>
      DateTime.dateEquals(selectedDateTime, new Date(v.startTime))
      && DateTime.timeEquals(selectedSlotTime, new Date(v.startTime))
    )
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

const pluralize = (v: number, singularText: string, pluralText: string) => {
  if (v === 1) {
    return `${v} ${singularText}`
  }
  return `${v} ${pluralText}`
}
</script>

<template>
    <div v-if="slotSummaries.length > 1">
        <h2 class="text-lg">Datum</h2>
        <div class="mt-2 flex flex-wrap gap-2">
        <template v-for="slotSummary in slotSummaries" :key="slotSummary.date">
            <button v-if="slotSummary.hasUnlimitedCapacity" type="button" class="!flex flex-col items-center justify-center button"
            :class="{ 'button-htlvb-selected': selectedDate === slotSummary.date }" @click="selectedDate = slotSummary.date">
            <span>{{ DateTime.formatDate(new Date(slotSummary.date), { weekday: 'short' }) }}</span>
            </button>
            <button v-else-if="slotSummary.freeSlots > 0" type="button" class="!flex flex-col items-center button"
            :class="{ 'button-htlvb-selected': selectedDate === slotSummary.date }" @click="selectedDate = slotSummary.date">
            <span>{{ DateTime.formatDate(new Date(slotSummary.date), { weekday: 'short' }) }}</span>
            <span class="text-sm">{{ pluralize(slotSummary.freeSlots, 'freier Platz', 'freie Plätze') }}</span>
            </button>
            <button v-else-if="slotSummary.hasClosedSlot" type="button" :disabled="true" class="!flex flex-col items-center button">
            <span>{{ DateTime.formatDate(new Date(slotSummary.date), { weekday: 'short' }) }}</span>
            <span class="text-sm">geschlossen</span>
            </button>
            <button v-else-if="slotSummary.hasTakenSlot" type="button" :disabled="true" class="!flex flex-col items-center button">
            <span>{{ DateTime.formatDate(new Date(slotSummary.date), { weekday: 'short' }) }}</span>
            <span class="text-sm">ausgebucht</span>
            </button>
        </template>
        </div>
    </div>
    <template v-if="selectedDate !== undefined">
        <h2 class="text-lg mt-4">Uhrzeit</h2>
        <div class="mt-2 flex flex-wrap gap-2">
        <template v-for="slot in slots.filter(v => selectedDate !== undefined && DateTime.dateEquals(new Date(selectedDate), new Date(v.startTime)))"
            :key="slot.startTime">
            <template v-if="slot.type.type === 'free'">
            <button v-if="slot.type.remainingCapacity === null" type="button" class="!flex flex-col items-center justify-center button"
                :class="{ 'button-htlvb-selected': selectedSlot === slot }" @click="selectedSlot = slot">
                <span>{{ formatSlotTime(slot) }}</span>
            </button>
            <button v-else type="button" class="!flex flex-col items-center button"
                :class="{ 'button-htlvb-selected': selectedSlot === slot }" @click="selectedSlot = slot">
                <span>{{ formatSlotTime(slot) }}</span>
                <span class="text-sm">{{ pluralize(slot.type.remainingCapacity, 'freier Platz', 'freie Plätze') }}</span>
            </button>
            </template>
            <button v-else-if="slot.type.type === 'closed'" type="button" :disabled="true" class="!flex flex-col items-center button">
            <span>{{ formatSlotTime(slot) }}</span>
            <span class="text-sm">geschlossen</span>
            </button>
            <button v-else-if="slot.type.type === 'taken'" type="button" :disabled="true" class="!flex flex-col items-center button">
            <span>{{ formatSlotTime(slot) }}</span>
            <span class="text-sm">ausgebucht</span>
            </button>
        </template>
        </div>
        <div v-if="selectedSlot !== undefined && selectedSlot.type.type === 'free' && selectedSlot.type.closingDate !== null" class="mt-2">
        <span class="text-sm">Anmeldeschluss: {{ formatClosingDate(new Date(selectedSlot.type.closingDate)) }}</span>
        </div>
    </template>
</template>