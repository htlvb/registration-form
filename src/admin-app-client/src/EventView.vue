<script setup lang="ts">
import { ref } from 'vue'
import type { Dto } from './DataTransfer'
import { DateTime } from './Utils';

defineProps<{
  event: Dto.Event
}>()

const isEditing = ref(false)
const isShowingDetails = ref(false)
</script>
<template>
  <div class="flex flex-col gap-2">
    <div class="flex items-center gap-2">
      <span>{{ event.title }}</span>
      <span v-if="!isEditing && !isShowingDetails">
        <a v-if="event.canEditEventData || event.canAddSlot || event.canEditSlot || event.canDeleteSlot"
          class="px-2 py-2 cursor-pointer"
          title="Bearbeiten"
          @click="isEditing = true">
          <i class="fa-solid fa-pencil"></i>
        </a>
        <a v-else class="px-2 py-2 cursor-pointer" title="Details anzeigen" @click="isShowingDetails = true">
          <i class="fa-solid fa-eye"></i>
        </a>
        <a class="px-2 py-2 cursor-pointer" title="Duplizieren">
          <i class="fa-regular fa-clone"></i>
        </a>
      </span>
    </div>
    <div v-if="isShowingDetails">
      <div class="flex flex-col">
        <span class="text-sm">Infotext:</span>
        <span class="ml-4">{{ event.infoText }}</span>
      </div>
      <div class="flex flex-col">
        <span class="text-sm">Registrierung möglich ab:</span>
        <span class="ml-4">{{ DateTime.format(new Date(event.reservationStartTime)) }}</span>
      </div>
      <div class="flex flex-col">
        <span class="text-sm">Slots:</span>
        <ol class="ml-8 list-decimal">
          <li v-for="slot in event.slots">
            {{ DateTime.format(new Date(slot.startTime)) }}<br />
            <span class="text-sm">Ende: {{ slot.duration !== null ? DateTime.format(DateTime.addTimeSpan(new Date(slot.startTime), slot.duration)) : "keine Angabe" }}</span><br />
            <span class="text-sm">Anmeldeschluss: {{ slot.closingDate !== null ? DateTime.format(new Date(slot.closingDate)) : "-" }}</span><br />
            <span class="text-sm">Freie Plätze: {{ slot.remainingCapacity !== null ? slot.remainingCapacity : "unbegrenzt" }}</span><br />
            <span class="text-sm">Maximale Personen pro Registrierung: {{ slot.maxQuantityPerBooking !== null ? slot.maxQuantityPerBooking : "unbegrenzt" }}</span><br />
          </li>
        </ol>
      </div>
    </div>
  </div>
</template>