<script setup lang="ts">
import * as DataTransfer from '@/DataTransfer.ts'
import EventView from './EventView.vue'

defineProps<{
  events: DataTransfer.Event[]
}>()

defineEmits<{
  eventUpdated: []
}>()
</script>
<template>
  <div v-if="events.length === 0">Keine Events vorhanden.</div>
  <div v-else class="flex flex-col gap-2">
    <EventView v-for="event in events" :key="event.key" :event="event" @event-updated="$emit('eventUpdated')" />
  </div>
</template>